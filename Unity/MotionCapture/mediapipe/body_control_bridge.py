import argparse
import math
import socket
import struct
import time


JOINT_COUNT = 10

PELVIS = 0
TORSO = 1
LEFT_SHOULDER = 2
RIGHT_SHOULDER = 3
LEFT_ELBOW = 4
RIGHT_ELBOW = 5
LEFT_HIP = 6
RIGHT_HIP = 7
LEFT_KNEE = 8
RIGHT_KNEE = 9

UPPER_JOINTS = (TORSO, LEFT_SHOULDER, RIGHT_SHOULDER, LEFT_ELBOW, RIGHT_ELBOW)
LOWER_JOINTS = (PELVIS, LEFT_HIP, RIGHT_HIP, LEFT_KNEE, RIGHT_KNEE)
LEFT_JOINTS = (LEFT_SHOULDER, LEFT_ELBOW, LEFT_HIP, LEFT_KNEE)
RIGHT_JOINTS = (RIGHT_SHOULDER, RIGHT_ELBOW, RIGHT_HIP, RIGHT_KNEE)
HEIGHT_PROXY_JOINTS = (PELVIS, TORSO)

SIGNAL_ORDER = (
    "/body/energy",
    "/body/stillness",
    "/body/asymmetry",
    "/body/height",
    "/body/upper",
    "/body/lower",
    "/body/pulse",
    "/body/presence",
)

MIDI_SIGNAL_TO_ADDRESS = {
    "energy": "/body/energy",
    "stillness": "/body/stillness",
    "presence": "/body/presence",
    "pulse": "/body/pulse",
    "asymmetry": "/body/asymmetry",
    "height": "/body/height",
}

UNITY_SIGNAL_NAMES = (
    "energy",
    "stillness",
    "presence",
    "pulse",
    "asymmetry",
    "height",
    "upper",
    "lower",
)


def clamp01(value):
    return max(0.0, min(1.0, value))


def float_to_midi_cc(value):
    return int(round(clamp01(value) * 127.0))


def parse_mprot(payload):
    payload = payload.replace("<EOM>", "")
    lines = payload.strip().splitlines()
    if not lines or lines[0].strip() != "mprot":
        return None

    joints = {}
    for line in lines[1:]:
        parts = line.split("|")
        if len(parts) != 6:
            continue
        try:
            joint = int(parts[0])
            qx, qy, qz, qw = map(float, parts[1:5])
            visibility = float(parts[5])
        except ValueError:
            continue
        joints[joint] = (quat_normalize((qx, qy, qz, qw)), clamp01(visibility))
    return joints


def quat_dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3]


def quat_normalize(q):
    length = math.sqrt(max(0.0, quat_dot(q, q)))
    if length <= 1e-8:
        return (0.0, 0.0, 0.0, 1.0)
    return (q[0] / length, q[1] / length, q[2] / length, q[3] / length)


def quat_angle(a, b):
    dot = abs(quat_dot(quat_normalize(a), quat_normalize(b)))
    dot = max(-1.0, min(1.0, dot))
    return 2.0 * math.acos(dot)


def mean(values):
    values = list(values)
    if not values:
        return 0.0
    return sum(values) / len(values)


def mean_visibility(joints, joint_ids):
    return mean(joints[j][1] for j in joint_ids if j in joints)


def joint_speed(current, previous, joint_id, dt):
    if not previous or joint_id not in current or joint_id not in previous or dt <= 1e-5:
        return 0.0
    angle = quat_angle(current[joint_id][0], previous[joint_id][0])
    return angle / dt


def group_activity(current, previous, joint_ids, dt, speed_scale):
    speeds = []
    for joint_id in joint_ids:
        if joint_id in current:
            visibility = current[joint_id][1]
            speeds.append(joint_speed(current, previous, joint_id, dt) * visibility)
    return clamp01(mean(speeds) / max(0.001, speed_scale))


def height_proxy(current):
    # mprot contains rotations, not world-space joint positions. Treat this as an
    # upright / folded posture proxy until a position stream is added.
    angles = []
    for joint_id in HEIGHT_PROXY_JOINTS:
        if joint_id in current:
            angle_from_identity = quat_angle(current[joint_id][0], (0.0, 0.0, 0.0, 1.0))
            angles.append(angle_from_identity * current[joint_id][1])
    folded = clamp01(mean(angles) / 1.4)
    return 1.0 - folded


def osc_pad(data):
    padding = (4 - (len(data) % 4)) % 4
    if padding == 0:
        padding = 4
    return data + (b"\0" * padding)


def osc_message(address, value):
    address_blob = osc_pad(address.encode("utf-8") + b"\0")
    tag_blob = osc_pad(b",f\0")
    return address_blob + tag_blob + struct.pack(">f", clamp01(float(value)))


def unity_control_message(signals):
    lines = ["wctrl"]
    for name in UNITY_SIGNAL_NAMES:
        value = clamp01(signals.get(f"/body/{name}", 0.0))
        lines.append(f"{name}|{value:.6f}")
    return "\n".join(lines) + "\n"


class MidiCCOutput:
    def __init__(self, args):
        self.args = args
        self.last_send_time = 0.0
        self.last_values = {}
        self.port_name = self.resolve_port_name(args.midi_port_name)
        self.port = self.open_port(self.port_name)
        self.channel = max(1, min(16, args.midi_channel)) - 1
        self.cc_map = {
            "energy": args.cc_energy,
            "stillness": args.cc_stillness,
            "presence": args.cc_presence,
            "pulse": args.cc_pulse,
            "asymmetry": args.cc_asymmetry,
            "height": args.cc_height,
        }

    def resolve_port_name(self, requested_name):
        mido = import_mido()
        try:
            available_ports = mido.get_output_names()
        except Exception as exc:
            raise RuntimeError(
                "Could not query MIDI output ports. Check that python-rtmidi is installed "
                "and that the system MIDI service is available."
            ) from exc

        if not requested_name:
            raise RuntimeError(
                "MIDI output requires --midi-port-name. Available MIDI output ports:\n"
                + format_midi_ports(available_ports)
            )

        exact_match = find_midi_port_match(requested_name, available_ports)
        if exact_match:
            if exact_match != requested_name:
                print(f'[body-bridge] MIDI port alias "{requested_name}" matched "{exact_match}"')
            return exact_match

        raise RuntimeError(
            f'MIDI output port "{requested_name}" was not found. Available MIDI output ports:\n'
            + format_midi_ports(available_ports)
            + "\nTip: on localized macOS systems, IAC may appear as a translated or mojibake name. "
            + 'Try --midi-port-name "IAC" or copy one available port name exactly.'
        )

    def open_port(self, port_name):
        mido = import_mido()

        try:
            return mido.open_output(port_name)
        except Exception as exc:
            raise RuntimeError(f'Could not open MIDI output port "{port_name}": {exc}') from exc

    def maybe_send(self, signals, now):
        min_period = 1.0 / max(1.0, self.args.midi_send_rate)
        if now - self.last_send_time < min_period:
            return

        sent_any = False
        for name, address in MIDI_SIGNAL_TO_ADDRESS.items():
            value = clamp01(signals.get(address, 0.0))
            if name == "pulse":
                value = 1.0 if value >= 0.5 else 0.0

            previous = self.last_values.get(name)
            if previous is not None and abs(value - previous) < self.args.midi_change_threshold:
                continue

            self.send_cc(self.cc_map[name], float_to_midi_cc(value))
            self.last_values[name] = value
            sent_any = True

        if sent_any:
            self.last_send_time = now

    def send_cc(self, control, value):
        import mido

        control = max(0, min(127, int(control)))
        value = max(0, min(127, int(value)))
        message = mido.Message("control_change", channel=self.channel, control=control, value=value)
        self.port.send(message)


class UnityUDPOutput:
    def __init__(self, args):
        self.args = args
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.last_send_time = 0.0

    def maybe_send(self, signals, now):
        min_period = 1.0 / max(1.0, self.args.unity_send_rate)
        if now - self.last_send_time < min_period:
            return

        payload = unity_control_message(signals).encode("utf-8")
        self.sock.sendto(payload, (self.args.unity_host, self.args.unity_port))
        self.last_send_time = now


def format_midi_ports(port_names):
    if not port_names:
        return "  <none>"
    return "\n".join(f"  - {name}" for name in port_names)


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

    if is_iac_bus_request(requested_name):
        requested_bus_number = extract_last_digit(requested_name)
        iac_candidates = [name for name in available_ports if "iac" in name.casefold()]
        if requested_bus_number:
            for port_name in iac_candidates:
                if extract_last_digit(port_name) == requested_bus_number:
                    return port_name
        if len(iac_candidates) == 1:
            return iac_candidates[0]

    return None


def is_iac_bus_request(name):
    normalized = normalized_midi_name(name)
    return normalized in ("iac", "iacdriver", "iacdriverbus", "iacdriverbus1") or normalized.startswith("iacdriverbus")


def extract_last_digit(name):
    digits = [ch for ch in name if ch.isdigit()]
    return digits[-1] if digits else None


def print_available_midi_ports():
    mido = import_mido()
    print("Available MIDI output ports:")
    print(format_midi_ports(mido.get_output_names()))


class BodyControlBridge:
    def __init__(self, args):
        self.args = args
        self.previous_joints = None
        self.previous_time = None
        self.previous_energy = 0.0
        self.pulse_value = 0.0
        self.last_pulse_time = 0.0
        self.smoothed = {signal: 0.0 for signal in SIGNAL_ORDER}
        self.smoothed["/body/stillness"] = 1.0
        self.smoothed["/body/height"] = 1.0

    def compute_signals(self, joints, now):
        dt = 1.0 / self.args.target_hz
        if self.previous_time is not None:
            dt = max(1e-5, min(0.25, now - self.previous_time))

        upper = group_activity(joints, self.previous_joints, UPPER_JOINTS, dt, self.args.speed_scale)
        lower = group_activity(joints, self.previous_joints, LOWER_JOINTS, dt, self.args.speed_scale)
        energy = group_activity(joints, self.previous_joints, range(JOINT_COUNT), dt, self.args.speed_scale)

        left = group_activity(joints, self.previous_joints, LEFT_JOINTS, dt, self.args.speed_scale)
        right = group_activity(joints, self.previous_joints, RIGHT_JOINTS, dt, self.args.speed_scale)
        asymmetry = clamp01(abs(left - right) * 2.0)

        presence = mean_visibility(joints, range(JOINT_COUNT))
        height = height_proxy(joints)

        if (
            energy - self.previous_energy >= self.args.pulse_threshold
            and now - self.last_pulse_time >= self.args.pulse_cooldown
            and presence >= self.args.presence_gate
        ):
            self.pulse_value = 1.0
            self.last_pulse_time = now
        else:
            decay = math.exp(-dt / max(0.001, self.args.pulse_decay))
            self.pulse_value *= decay

        raw = {
            "/body/energy": energy,
            "/body/stillness": 1.0 - energy,
            "/body/asymmetry": asymmetry,
            "/body/height": height,
            "/body/upper": upper,
            "/body/lower": lower,
            "/body/pulse": self.pulse_value,
            "/body/presence": presence,
        }

        alpha = 1.0 - math.exp(-dt / max(0.001, self.args.smoothing))
        for signal, value in raw.items():
            if signal == "/body/pulse":
                self.smoothed[signal] = value
            else:
                self.smoothed[signal] += (value - self.smoothed[signal]) * alpha
                self.smoothed[signal] = clamp01(self.smoothed[signal])

        self.previous_joints = joints
        self.previous_time = now
        self.previous_energy = energy
        return self.smoothed


def build_arg_parser():
    parser = argparse.ArgumentParser(
        description="Convert collective mprot pose packets into smoothed OSC and/or MIDI CC body controls for VCV Rack."
    )
    parser.add_argument("--listen-host", default="0.0.0.0")
    parser.add_argument("--listen-port", type=int, default=53100)
    parser.add_argument("--output-mode", choices=("osc", "midi", "both"), default="osc")
    parser.add_argument("--vcv-host", default="127.0.0.1")
    parser.add_argument("--vcv-port", type=int, default=54000)
    parser.add_argument("--midi-port-name", default="")
    parser.add_argument("--midi-channel", type=int, default=1)
    parser.add_argument("--list-midi-ports", action="store_true")
    parser.add_argument("--midi-send-rate", type=float, default=30.0)
    parser.add_argument("--midi-change-threshold", type=float, default=0.01)
    parser.add_argument("--cc-energy", type=int, default=20)
    parser.add_argument("--cc-stillness", type=int, default=21)
    parser.add_argument("--cc-presence", type=int, default=22)
    parser.add_argument("--cc-pulse", type=int, default=23)
    parser.add_argument("--cc-asymmetry", type=int, default=24)
    parser.add_argument("--cc-height", type=int, default=25)
    parser.add_argument("--unity-output", action="store_true")
    parser.add_argument("--unity-host", default="127.0.0.1")
    parser.add_argument("--unity-port", type=int, default=55000)
    parser.add_argument("--unity-send-rate", type=float, default=25.0)
    parser.add_argument("--target-hz", type=float, default=25.0)
    parser.add_argument("--speed-scale", type=float, default=3.0)
    parser.add_argument("--smoothing", type=float, default=0.18)
    parser.add_argument("--presence-gate", type=float, default=0.2)
    parser.add_argument("--pulse-threshold", type=float, default=0.35)
    parser.add_argument("--pulse-cooldown", type=float, default=0.25)
    parser.add_argument("--pulse-decay", type=float, default=0.18)
    parser.add_argument("--quiet", action="store_true")
    return parser


def main():
    args = build_arg_parser().parse_args()

    if args.list_midi_ports:
        try:
            print_available_midi_ports()
        except RuntimeError as exc:
            print(f"[body-bridge] MIDI error: {exc}")
            raise SystemExit(2)
        return

    midi_output = None
    if args.output_mode in ("midi", "both"):
        try:
            midi_output = MidiCCOutput(args)
        except RuntimeError as exc:
            print(f"[body-bridge] MIDI error: {exc}")
            raise SystemExit(2)
    unity_output = UnityUDPOutput(args) if args.unity_output else None

    in_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    in_sock.bind((args.listen_host, args.listen_port))
    in_sock.setblocking(False)

    out_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    bridge = BodyControlBridge(args)

    min_period = 1.0 / max(1.0, args.target_hz)
    last_send = 0.0
    last_status = 0.0
    latest_joints = None
    latest_packet_time = 0.0

    print(f"[body-bridge] listening for mprot on {args.listen_host}:{args.listen_port}")
    print(f"[body-bridge] output mode: {args.output_mode}")
    if args.output_mode in ("osc", "both"):
        print(f"[body-bridge] sending OSC to {args.vcv_host}:{args.vcv_port}")
    if midi_output is not None:
        cc_status = ", ".join(f"{name}=CC{cc}" for name, cc in midi_output.cc_map.items())
        print(f"[body-bridge] sending MIDI CC to {midi_output.port_name}, channel {midi_output.channel + 1}")
        print(f"[body-bridge] MIDI CC map: {cc_status}")
    if unity_output is not None:
        print(f"[body-bridge] sending Unity Waterfall controls to {args.unity_host}:{args.unity_port}")
    print("[body-bridge] signals: " + ", ".join(SIGNAL_ORDER))

    while True:
        now = time.time()

        while True:
            try:
                data, _addr = in_sock.recvfrom(65535)
            except BlockingIOError:
                break

            text = data.decode("utf-8", errors="ignore")
            joints = parse_mprot(text)
            if joints is not None:
                latest_joints = joints
                latest_packet_time = now

        if latest_joints is None or now - latest_packet_time > 1.0:
            time.sleep(0.002)
            continue

        if now - last_send < min_period:
            time.sleep(0.001)
            continue

        signals = bridge.compute_signals(latest_joints, now)
        if args.output_mode in ("osc", "both"):
            for address in SIGNAL_ORDER:
                out_sock.sendto(osc_message(address, signals[address]), (args.vcv_host, args.vcv_port))

        if midi_output is not None:
            midi_output.maybe_send(signals, now)

        if unity_output is not None:
            unity_output.maybe_send(signals, now)

        if not args.quiet and now - last_status >= 1.0:
            status = " ".join(f"{address.split('/')[-1]}={signals[address]:.2f}" for address in SIGNAL_ORDER)
            print(f"[body-bridge] {status}")
            last_status = now

        last_send = now


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n[body-bridge] stopped")
