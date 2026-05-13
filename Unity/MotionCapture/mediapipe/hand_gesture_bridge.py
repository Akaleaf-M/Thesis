import argparse
import math
import socket
import struct
import sys
import time


UNITY_SIGNAL_NAMES = (
    "energy",
    "stillness",
    "presence",
    "pulse",
    "asymmetry",
    "height",
    "upper",
    "lower",
    "swipe",
)

cv2 = None
mp = None

OSC_SIGNAL_ORDER = (
    "/body/energy",
    "/body/stillness",
    "/body/asymmetry",
    "/body/height",
    "/body/upper",
    "/body/lower",
    "/body/pulse",
    "/body/presence",
    "/hand/left/point",
    "/hand/left/open",
    "/hand/right/point",
    "/hand/right/open",
    "/hand/swipe",
    "/hand/visible",
)

MIDI_SIGNAL_TO_CC = {
    "energy": 20,
    "stillness": 21,
    "presence": 22,
    "pulse": 23,
    "asymmetry": 24,
    "height": 25,
    "left_point": 26,
    "left_open": 27,
    "right_point": 28,
    "right_open": 29,
    "swipe": 30,
    "hand_visible": 31,
}

HAND_CONNECTIONS = (
    (0, 1), (1, 2), (2, 3), (3, 4),
    (0, 5), (5, 6), (6, 7), (7, 8),
    (0, 9), (9, 10), (10, 11), (11, 12),
    (0, 13), (13, 14), (14, 15), (15, 16),
    (0, 17), (17, 18), (18, 19), (19, 20),
)


def clamp01(value):
    return max(0.0, min(1.0, float(value)))


def lerp(a, b, t):
    return a + (b - a) * t


def distance(a, b):
    return math.sqrt((a.x - b.x) ** 2 + (a.y - b.y) ** 2 + (a.z - b.z) ** 2)


def palm_scale(landmarks):
    wrist = landmarks[0]
    index_mcp = landmarks[5]
    pinky_mcp = landmarks[17]
    scale = (distance(wrist, index_mcp) + distance(wrist, pinky_mcp) + distance(index_mcp, pinky_mcp)) / 3.0
    return max(0.03, scale)


def finger_extension(landmarks, tip, mcp, wrist_index=0):
    wrist = landmarks[wrist_index]
    tip_distance = distance(landmarks[tip], wrist)
    mcp_distance = distance(landmarks[mcp], wrist)
    return clamp01((tip_distance - mcp_distance * 0.72) / max(0.001, mcp_distance * 0.55))


def float_to_midi_cc(value):
    return int(round(clamp01(value) * 127.0))


def osc_pad(data):
    padding = (4 - (len(data) % 4)) % 4
    if padding == 0:
        padding = 4
    return data + (b"\0" * padding)


def osc_message(address, value):
    address_blob = osc_pad(address.encode("utf-8") + b"\0")
    tag_blob = osc_pad(b",f\0")
    return address_blob + tag_blob + struct.pack(">f", clamp01(value))


def unity_control_message(signals):
    lines = ["wctrl"]
    for name in UNITY_SIGNAL_NAMES:
        lines.append(f"{name}|{clamp01(signals.get(name, 0.0)):.6f}")
    return "\n".join(lines) + "\n"


def hand_landmark_message(hand_data, signals):
    lines = ["hland"]
    lines.append(f"hands|{len(hand_data)}")
    for name in ("point", "open", "fist", "left_point", "left_open", "right_point", "right_open", "pulse", "swipe", "hand_visible"):
        lines.append(f"gesture|{name}|{clamp01(signals.get(name, 0.0)):.6f}")

    for hand_index, hand in enumerate(hand_data):
        handedness = hand["handedness"]
        landmarks = hand["landmarks"]
        lines.append(f"hand|{hand_index}|{handedness}")
        for point_index, landmark in enumerate(landmarks):
            lines.append(
                f"{hand_index}|{point_index}|{landmark.x:.6f}|{landmark.y:.6f}|{landmark.z:.6f}"
            )
    return "\n".join(lines) + "\n"


def import_mido():
    try:
        import mido
    except ImportError as exc:
        raise RuntimeError(
            "MIDI output requires mido and python-rtmidi. Install with: "
            "python -m pip install mido python-rtmidi"
        ) from exc
    return mido


def normalized_midi_name(name):
    return "".join(ch for ch in name.casefold() if ch.isalnum())


def find_midi_port_match(requested_name, available_ports):
    if requested_name in available_ports:
        return requested_name

    requested_normalized = normalized_midi_name(requested_name)
    for port_name in available_ports:
        if normalized_midi_name(port_name) == requested_normalized:
            return port_name

    if requested_normalized.startswith("iac") or requested_normalized in ("iac", "iacdriver", "iacdriverbus1"):
        iac_candidates = [name for name in available_ports if "iac" in name.casefold()]
        if len(iac_candidates) == 1:
            return iac_candidates[0]

    return None


def format_midi_ports(port_names):
    if not port_names:
        return "  <none>"
    return "\n".join(f"  - {name}" for name in port_names)


class MidiCCOutput:
    def __init__(self, args):
        mido = import_mido()
        available_ports = mido.get_output_names()
        port_name = find_midi_port_match(args.midi_port_name, available_ports)
        if not port_name:
            raise RuntimeError(
                f'MIDI output port "{args.midi_port_name}" was not found. Available MIDI output ports:\n'
                + format_midi_ports(available_ports)
            )

        self.args = args
        self.mido = mido
        self.port_name = port_name
        self.port = mido.open_output(port_name)
        self.channel = max(1, min(16, args.midi_channel)) - 1
        self.last_send_time = 0.0
        self.last_values = {}

    def maybe_send(self, signals, now):
        min_period = 1.0 / max(1.0, self.args.midi_send_rate)
        if now - self.last_send_time < min_period:
            return

        sent_any = False
        for name, cc in MIDI_SIGNAL_TO_CC.items():
            value = clamp01(signals.get(name, 0.0))

            previous = self.last_values.get(name)
            if previous is not None and abs(value - previous) < self.args.midi_change_threshold:
                continue

            message = self.mido.Message(
                "control_change",
                channel=self.channel,
                control=max(0, min(127, int(cc))),
                value=float_to_midi_cc(value),
            )
            self.port.send(message)
            self.last_values[name] = value
            sent_any = True

        if sent_any:
            self.last_send_time = now


class GestureAnalyzer:
    def __init__(self, args):
        self.args = args
        self.previous_centers = {}
        self.previous_time = None
        self.previous_point = 0.0
        self.previous_energy = 0.0
        self.pulse_value = 0.0
        self.swipe_value = 0.0
        self.hand_visible_value = 0.0
        self.last_pulse_time = 0.0
        self.last_swipe_time = 0.0
        self.previous_hand_visible = False
        self.smoothed = {
            "energy": 0.0,
            "stillness": 1.0,
            "presence": 0.0,
            "pulse": 0.0,
            "asymmetry": 0.0,
            "height": 1.0,
            "upper": 0.0,
            "lower": 0.0,
            "point": 0.0,
            "open": 0.0,
            "fist": 0.0,
            "swipe": 0.0,
            "left_point": 0.0,
            "left_open": 0.0,
            "left_fist": 0.0,
            "right_point": 0.0,
            "right_open": 0.0,
            "right_fist": 0.0,
            "hand_visible": 0.0,
        }

    def compute(self, hand_data, now):
        dt = 1.0 / self.args.target_hz
        if self.previous_time is not None:
            dt = max(1e-5, min(0.25, now - self.previous_time))

        if not hand_data:
            if self.previous_hand_visible:
                self.hand_visible_value = 1.0
            else:
                self.hand_visible_value *= math.exp(-dt / max(0.001, self.args.event_decay))

            raw = dict(self.smoothed)
            raw.update({
                "energy": 0.0,
                "stillness": 1.0,
                "presence": 0.0,
                "pulse": 0.0,
                "upper": 0.0,
                "lower": 0.0,
                "point": 0.0,
                "open": 0.0,
                "fist": 0.0,
                "swipe": 0.0,
                "left_point": 0.0,
                "left_open": 0.0,
                "left_fist": 0.0,
                "right_point": 0.0,
                "right_open": 0.0,
                "right_fist": 0.0,
                "hand_visible": self.hand_visible_value,
            })
            self.previous_centers = {}
            self.previous_time = now
            self.previous_point = 0.0
            self.previous_energy = 0.0
            self.pulse_value = 0.0
            self.swipe_value = 0.0
            self.previous_hand_visible = False
            return self._smooth(raw, dt)

        metrics = {}
        next_centers = {}
        for hand in hand_data:
            side = self._hand_side(hand)
            hand_metrics = self._hand_metrics(hand["landmarks"])
            metrics[side] = hand_metrics
            next_centers[side] = hand_metrics["center"]

        speeds = []
        horizontal_speeds = []
        for side, center in next_centers.items():
            previous_center = self.previous_centers.get(side)
            if previous_center is None:
                continue

            movement = math.sqrt(
                (center[0] - previous_center[0]) ** 2
                + (center[1] - previous_center[1]) ** 2
                + (center[2] - previous_center[2]) ** 2
            )
            speeds.append(movement / dt)
            horizontal_speeds.append(abs(center[0] - previous_center[0]) / dt)

        energy = clamp01((sum(speeds) / len(speeds)) / max(0.001, self.args.speed_scale)) if speeds else 0.0
        left = metrics.get("left", {})
        right = metrics.get("right", {})
        left_open = left.get("open", 0.0)
        right_open = right.get("open", 0.0)
        left_point = left.get("point", 0.0)
        right_point = right.get("point", 0.0)
        open_hand = max(left_open, right_open)
        point = max(left_point, right_point)
        fist = 1.0 - open_hand
        centers = [hand_metrics["center"] for hand_metrics in metrics.values()]
        average_center = self._average_center(centers)
        horizontal_speed = max(horizontal_speeds) if horizontal_speeds else 0.0
        swipe_triggered = horizontal_speed >= self.args.swipe_threshold and now - self.last_swipe_time >= self.args.swipe_cooldown
        if swipe_triggered:
            self.swipe_value = 1.0
            self.last_swipe_time = now
        else:
            self.swipe_value *= math.exp(-dt / max(0.001, self.args.event_decay))

        point_triggered = point >= self.args.point_trigger_threshold and self.previous_point < self.args.point_trigger_threshold
        energy_triggered = energy - self.previous_energy >= self.args.pulse_threshold
        if (point_triggered or energy_triggered) and now - self.last_pulse_time >= self.args.pulse_cooldown:
            self.pulse_value = 1.0
            self.last_pulse_time = now
        else:
            self.pulse_value *= math.exp(-dt / max(0.001, self.args.event_decay))

        if not self.previous_hand_visible:
            self.hand_visible_value = 1.0
        else:
            self.hand_visible_value *= math.exp(-dt / max(0.001, self.args.event_decay))

        raw = {
            "energy": energy,
            "stillness": 1.0 - energy,
            "presence": 1.0,
            "pulse": self.pulse_value,
            "asymmetry": clamp01(abs(left_open - right_open) + abs(left_point - right_point)),
            "height": clamp01(1.0 - average_center[1]),
            "upper": open_hand,
            "lower": fist,
            "point": point,
            "open": open_hand,
            "fist": fist,
            "swipe": self.swipe_value,
            "left_point": left_point,
            "left_open": left_open,
            "left_fist": left.get("fist", 0.0),
            "right_point": right_point,
            "right_open": right_open,
            "right_fist": right.get("fist", 0.0),
            "hand_visible": self.hand_visible_value,
        }

        self.previous_centers = next_centers
        self.previous_time = now
        self.previous_point = point
        self.previous_energy = energy
        self.previous_hand_visible = True
        return self._smooth(raw, dt)

    def _hand_side(self, hand):
        handedness = str(hand.get("handedness", "")).strip().casefold()
        if handedness == "left":
            return "left"
        if handedness == "right":
            return "right"

        center = self._hand_center(hand["landmarks"])
        return "left" if center[0] < 0.5 else "right"

    def _hand_metrics(self, landmarks):
        center = self._hand_center(landmarks)
        extensions = [
            finger_extension(landmarks, 8, 5),
            finger_extension(landmarks, 12, 9),
            finger_extension(landmarks, 16, 13),
            finger_extension(landmarks, 20, 17),
        ]
        index_extension, middle_extension, ring_extension, pinky_extension = extensions
        open_hand = clamp01(sum(extensions) / len(extensions))
        fist = clamp01(1.0 - open_hand)
        folded_non_index = clamp01(1.0 - ((middle_extension + ring_extension + pinky_extension) / 3.0))
        point = clamp01(index_extension * folded_non_index)
        return {
            "center": center,
            "open": open_hand,
            "fist": fist,
            "point": point,
        }

    def _hand_center(self, landmarks):
        points = [landmarks[index] for index in (0, 5, 9, 13, 17)]
        return (
            sum(point.x for point in points) / len(points),
            sum(point.y for point in points) / len(points),
            sum(point.z for point in points) / len(points),
        )

    def _average_center(self, centers):
        if not centers:
            return (0.5, 0.5, 0.0)
        return (
            sum(center[0] for center in centers) / len(centers),
            sum(center[1] for center in centers) / len(centers),
            sum(center[2] for center in centers) / len(centers),
        )

    def _smooth(self, raw, dt):
        alpha = 1.0 - math.exp(-dt / max(0.001, self.args.smoothing))
        for name, value in raw.items():
            if name in ("pulse", "swipe", "hand_visible"):
                self.smoothed[name] = clamp01(value)
            else:
                self.smoothed[name] = clamp01(lerp(self.smoothed.get(name, 0.0), value, alpha))
        return self.smoothed


def read_hands(results):
    if not results.multi_hand_landmarks:
        return []

    hands = []
    for index, hand_landmarks in enumerate(results.multi_hand_landmarks):
        handedness = "Unknown"
        if results.multi_handedness and index < len(results.multi_handedness):
            handedness = results.multi_handedness[index].classification[0].label

        hands.append({
            "handedness": handedness,
            "landmarks": hand_landmarks.landmark,
        })
    return hands


def draw_status(image, signals):
    lines = [
        f"L point {signals['left_point']:.2f}  L open {signals['left_open']:.2f}",
        f"R point {signals['right_point']:.2f}  R open {signals['right_open']:.2f}",
        f"visible {signals['hand_visible']:.0f}  energy {signals['energy']:.2f}  pulse {signals['pulse']:.2f}  swipe {signals['swipe']:.2f}",
    ]
    y = 24
    for line in lines:
        cv2.putText(image, line, (12, y), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1, cv2.LINE_AA)
        y += 22


def list_midi_ports():
    mido = import_mido()
    print("Available MIDI output ports:")
    print(format_midi_ports(mido.get_output_names()))


def build_arg_parser():
    parser = argparse.ArgumentParser(
        description="Track one performer's hands and send gesture controls to WaterfallA, VCV OSC, and/or VCV MIDI CC."
    )
    parser.add_argument("--camera-id", type=int, default=0)
    parser.add_argument("--max-hands", type=int, default=2)
    parser.add_argument("--model-complexity", type=int, default=1)
    parser.add_argument("--min-detection-confidence", type=float, default=0.72)
    parser.add_argument("--min-tracking-confidence", type=float, default=0.55)
    parser.add_argument("--output-mode", choices=("osc", "midi", "both", "unity"), default="both")
    parser.add_argument("--vcv-host", default="127.0.0.1")
    parser.add_argument("--vcv-port", type=int, default=54000)
    parser.add_argument("--midi-port-name", default="IAC")
    parser.add_argument("--midi-channel", type=int, default=1)
    parser.add_argument("--midi-send-rate", type=float, default=25.0)
    parser.add_argument("--midi-change-threshold", type=float, default=0.01)
    parser.add_argument("--list-midi-ports", action="store_true")
    parser.add_argument("--unity-output", action="store_true", default=True)
    parser.add_argument("--no-unity-output", dest="unity_output", action="store_false")
    parser.add_argument("--unity-host", default="127.0.0.1")
    parser.add_argument("--unity-port", type=int, default=55000)
    parser.add_argument("--unity-send-rate", type=float, default=25.0)
    parser.add_argument("--hand-output", action="store_true")
    parser.add_argument("--hand-port", type=int, default=55010)
    parser.add_argument("--hand-send-rate", type=float, default=60.0)
    parser.add_argument("--target-hz", type=float, default=25.0)
    parser.add_argument("--speed-scale", type=float, default=1.8)
    parser.add_argument("--smoothing", type=float, default=0.14)
    parser.add_argument("--pulse-threshold", type=float, default=0.35)
    parser.add_argument("--pulse-cooldown", type=float, default=0.28)
    parser.add_argument("--point-trigger-threshold", type=float, default=0.72)
    parser.add_argument("--swipe-threshold", type=float, default=1.85)
    parser.add_argument("--swipe-cooldown", type=float, default=0.9)
    parser.add_argument("--event-decay", type=float, default=0.16)
    parser.add_argument("--no-preview", action="store_true")
    parser.add_argument("--quiet", action="store_true")
    return parser


def main():
    global cv2, mp
    args = build_arg_parser().parse_args()

    if args.list_midi_ports:
        list_midi_ports()
        return

    import cv2 as cv2_module
    import mediapipe as mp_module

    cv2 = cv2_module
    mp = mp_module

    midi_output = None
    if args.output_mode in ("midi", "both"):
        midi_output = MidiCCOutput(args)

    osc_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    unity_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    hand_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    if sys.platform.startswith("darwin"):
        cap = cv2.VideoCapture(args.camera_id, cv2.CAP_AVFOUNDATION)
    elif sys.platform.startswith("win"):
        cap = cv2.VideoCapture(args.camera_id, cv2.CAP_DSHOW)
    else:
        cap = cv2.VideoCapture(args.camera_id)

    if not cap.isOpened():
        raise RuntimeError(f"Failed to open camera index {args.camera_id}")

    analyzer = GestureAnalyzer(args)
    mp_hands = mp.solutions.hands
    mp_drawing = mp.solutions.drawing_utils
    min_period = 1.0 / max(1.0, args.target_hz)
    unity_min_period = 1.0 / max(1.0, args.unity_send_rate)
    hand_min_period = 1.0 / max(1.0, args.hand_send_rate)
    last_send = 0.0
    last_unity_send = 0.0
    last_hand_send = 0.0
    last_status = 0.0

    print(f"[hand-gesture] camera={args.camera_id}")
    print(f"[hand-gesture] output mode: {args.output_mode}")
    if args.output_mode in ("osc", "both"):
        print(f"[hand-gesture] sending OSC to {args.vcv_host}:{args.vcv_port}")
    if midi_output is not None:
        print(f"[hand-gesture] sending MIDI CC to {midi_output.port_name}, channel {midi_output.channel + 1}")
        print("[hand-gesture] MIDI CC map: " + ", ".join(f"{name}=CC{cc}" for name, cc in MIDI_SIGNAL_TO_CC.items()))
    if args.unity_output:
        print(f"[hand-gesture] sending WaterfallA wctrl to {args.unity_host}:{args.unity_port}")
    if args.hand_output:
        print(f"[hand-gesture] sending hand landmarks to {args.unity_host}:{args.hand_port}")

    with mp_hands.Hands(
        max_num_hands=max(1, args.max_hands),
        model_complexity=args.model_complexity,
        min_detection_confidence=args.min_detection_confidence,
        min_tracking_confidence=args.min_tracking_confidence,
    ) as hands:
        while cap.isOpened():
            success, image = cap.read()
            if not success:
                break

            image = cv2.flip(image, 1)
            rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
            results = hands.process(rgb)
            hand_data = read_hands(results)
            now = time.time()
            signals = analyzer.compute(hand_data, now)

            if now - last_send >= min_period:
                if args.output_mode in ("osc", "both"):
                    osc_values = {
                        "/body/energy": signals["energy"],
                        "/body/stillness": signals["stillness"],
                        "/body/asymmetry": signals["asymmetry"],
                        "/body/height": signals["height"],
                        "/body/upper": signals["upper"],
                        "/body/lower": signals["lower"],
                        "/body/pulse": signals["pulse"],
                        "/body/presence": signals["presence"],
                        "/hand/left/point": signals["left_point"],
                        "/hand/left/open": signals["left_open"],
                        "/hand/right/point": signals["right_point"],
                        "/hand/right/open": signals["right_open"],
                        "/hand/swipe": signals["swipe"],
                        "/hand/visible": signals["hand_visible"],
                    }
                    for address in OSC_SIGNAL_ORDER:
                        osc_sock.sendto(osc_message(address, osc_values[address]), (args.vcv_host, args.vcv_port))

                if midi_output is not None:
                    midi_output.maybe_send(signals, now)

                last_send = now

            if args.unity_output and now - last_unity_send >= unity_min_period:
                unity_sock.sendto(
                    unity_control_message(signals).encode("utf-8"),
                    (args.unity_host, args.unity_port),
                )
                last_unity_send = now

            if args.hand_output and now - last_hand_send >= hand_min_period:
                hand_sock.sendto(
                    hand_landmark_message(hand_data, signals).encode("utf-8"),
                    (args.unity_host, args.hand_port),
                )
                last_hand_send = now

            if not args.quiet and now - last_status >= 1.0:
                print(
                    "[hand-gesture] "
                    + " ".join(
                        f"{name}={signals[name]:.2f}"
                        for name in ("energy", "presence", "hand_visible", "pulse", "swipe", "left_point", "left_open", "right_point", "right_open")
                    )
                )
                last_status = now

            if not args.no_preview:
                if results.multi_hand_landmarks:
                    for hand_landmarks in results.multi_hand_landmarks:
                        mp_drawing.draw_landmarks(image, hand_landmarks, mp_hands.HAND_CONNECTIONS)
                draw_status(image, signals)
                cv2.imshow("Sonic Arts Hand Gesture Bridge", image)
                if cv2.waitKey(5) & 0xFF == 27:
                    break

    cap.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n[hand-gesture] stopped")
