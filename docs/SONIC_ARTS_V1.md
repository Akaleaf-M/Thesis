# Sonic Arts v1

This document describes the simplified Sonic Arts performance branch.

The v1 target is:

```text
single camera
-> MediaPipe Hands
-> hand_gesture_bridge.py
-> VCV MIDI CC and/or OSC
-> Unity WaterfallA wctrl
-> optional Unity hand landmark visualizer
```

This path does not replace the thesis UPose / aggregator / fragment pipeline. It is a sidecar performance mode for one performer.

## Main Files

- `Unity/MotionCapture/mediapipe/hand_gesture_bridge.py`
- `Unity/UPose/Assets/Scripts/WaterfallBodyControlReceiver.cs`
- `Unity/UPose/Assets/Scripts/HandLandmarkVisualizer.cs`
- `Unity/UPose/Assets/Scripts/WaterfallController.cs`

## Unity Setup

Open:

```text
Unity/UPose/Assets/Scenes/DanceScene.unity
```

For WaterfallA:

1. Select `OutputModeManager`.
2. Enable `Use Editor Preview Mode`.
3. Set `Editor Preview Mode` to `SonicArts`.
4. Enter Play Mode.
5. Confirm Console prints:

```text
[WaterfallBodyControlReceiver] Listening on UDP 0.0.0.0:55000
```

`WaterfallBodyControlReceiver` already receives `wctrl` packets on UDP `55000` and applies them to WaterfallA speed, density, brightness, glitch, accent, freeze, labels, pulse, and recomposition.

`SonicArts` output mode uses a `1920 x 1080` window and the `SonicArts` Waterfall preset. The preset uses the WaterfallA vertical visual language with a 16:9 world layout.

For a manually built player, launch with:

```bash
./SonicArts.app/Contents/MacOS/UPose --mode SonicArts
```

## Optional Hand Visualizer

`HandLandmarkVisualizer` draws a hand from primitive GameObjects:

- 21 sphere joints per hand
- line renderers for the MediaPipe hand skeleton
- color response for point / fist gestures

To use it:

1. `DanceScene` now has `HandLandmarkVisualizer` on `BackgroundRoot`.
2. Keep `Port = 55010`.
3. Start `hand_gesture_bridge.py` with `--hand-output`.
4. Press `H` in the Unity player to toggle the hand layer on / off.

The hand visualizer is optional. WaterfallA and VCV control work without it.

## Unity Event Controls

`WaterfallBodyControlReceiver` now also reads:

- `pulse`: triggers `WaterfallController.TriggerPulse(...)` and `RecomposeVerticalStreams(...)` while WaterfallA is active.
- `swipe`: toggles `WaterfallController` between `WaterfallA` and `WaterfallB`.

Useful Unity fields:

| Field | Use |
| --- | --- |
| `recomposeOnPulse` | Enables pulse-driven WaterfallA stream recomposition |
| `pulseRecomposeAmount` | How strongly each pulse rearranges vertical streams |
| `switchWaterfallModeOnSwipe` | Enables WaterfallA/B switching from swipe |
| `swipeTriggerThreshold` | Minimum incoming swipe value needed to switch |
| `swipeSwitchCooldown` | Minimum seconds between WaterfallA/B switches |

## Python Startup

From:

```bash
cd Unity/MotionCapture/mediapipe
```

List MIDI ports:

```bash
python hand_gesture_bridge.py --list-midi-ports
```

Start WaterfallA + VCV MIDI + OSC:

```bash
python hand_gesture_bridge.py --camera-id 0 --output-mode both --midi-port-name "IAC" --unity-output
```

Start WaterfallA only, with no VCV dependency:

```bash
python hand_gesture_bridge.py --camera-id 0 --output-mode unity --unity-output
```

Start WaterfallA + VCV + Unity hand visualizer:

```bash
python hand_gesture_bridge.py --camera-id 0 --output-mode both --midi-port-name "IAC" --unity-output --hand-output --hand-send-rate 60
```

Press `Esc` in the OpenCV preview window to stop.

## Ports

| Purpose | Port |
| --- | --- |
| VCV OSC input | `54000` |
| Unity WaterfallA `wctrl` input | `55000` |
| Unity hand landmark visualizer input | `55010` |

These ports are separate from the thesis UPose ports.

## Control Signals

The bridge sends the existing WaterfallA `wctrl` packet:

```text
wctrl
energy|0.0-1.0
stillness|0.0-1.0
presence|0.0-1.0
pulse|0.0-1.0
asymmetry|0.0-1.0
height|0.0-1.0
upper|0.0-1.0
lower|0.0-1.0
```

Current hand-to-body mapping:

| Signal | Source | WaterfallA use |
| --- | --- | --- |
| `presence` | at least one hand detected | brightness / density gate |
| `energy` | hand center movement speed | stream speed, density, intensity |
| `stillness` | inverse of energy | freeze tendency |
| `height` | hand vertical position in camera frame | stored for labels / future mapping |
| `asymmetry` | distance from screen center on X axis | bidirectional streams, glitch, recomposition |
| `pulse` | point onset or sudden hand motion | WaterfallA pulse and vertical stream recomposition |
| `upper` | strongest open-hand amount from either hand | accent bias |
| `lower` | inverse of strongest open-hand amount | visual contraction / internal debug value |

Additional OSC addresses:

| OSC address | Meaning |
| --- | --- |
| `/hand/left/point` | left-hand index finger extended while the other fingers are folded |
| `/hand/left/open` | left-hand open / fist amount, where fist is `0.0` and open palm is `1.0` |
| `/hand/right/point` | right-hand index finger extended while the other fingers are folded |
| `/hand/right/open` | right-hand open / fist amount, where fist is `0.0` and open palm is `1.0` |
| `/hand/swipe` | fast horizontal hand motion; event channel for WaterfallA/B switching |

## MIDI CC Mapping

Default MIDI CC output:

| Signal | CC |
| --- | --- |
| `energy` | `20` |
| `stillness` | `21` |
| `presence` | `22` |
| `pulse` | `23` |
| `asymmetry` | `24` |
| `height` | `25` |
| `left point` | `26` |
| `left open / fist` | `27` |
| `right point` | `28` |
| `right open / fist` | `29` |
| `swipe` | `30` |

Suggested first VCV assignments:

| Gesture / signal | Musical role |
| --- | --- |
| `energy / CC20` | filter cutoff, clock density, modulation depth |
| `presence / CC22` | global body-influence amount |
| `pulse / CC23` | WaterfallA recomposition, ratchet, glitch gate, sample-and-hold trigger |
| `asymmetry / CC24` | stereo spread or left/right modulation imbalance |
| `left point / CC26` | left-hand select, freeze, or routing focus |
| `left open / fist / CC27` | `0V` when left fist, `10V` when left open; use as one expand-contract macro |
| `right point / CC28` | right-hand select, freeze, or routing focus |
| `right open / fist / CC29` | `0V` when right fist, `10V` when right open; use as one expand-contract macro |
| `swipe / CC30` | WaterfallA/B switching gesture; can also trigger VCV scene fills |

Keep modulation depths small first. The VCV patch should remain self-generating; gestures should steer it rather than perform every note directly.

## Gesture Logic

MediaPipe Hands gives 21 normalized landmarks per hand. The bridge uses relative distances, so it should tolerate the performer moving closer or farther from the camera.

Current gesture detection:

- `left point` / `right point`: index finger is extended while middle, ring, and pinky are folded.
- `left open` / `right open`: index, middle, ring, and pinky tips are extended away from the wrist. This is the merged open / fist control: fist produces `0.0`, open palm produces `1.0`.
- `pulse`: fires on point onset or sudden energy jump. Unity uses it to trigger `WaterfallA` pulse and vertical stream recomposition.
- `swipe`: fast horizontal hand motion. Unity uses it to toggle between `WaterfallA` and `WaterfallB`.

Recommended performance vocabulary:

| Gesture | Sonic role | Visual role |
| --- | --- | --- |
| slow hand drift | subtle parameter steering | slow stream speed / density shift |
| raised hand | brighter / higher-register influence | higher `height` value for labels / future mapping |
| left point | left-hand select, grab, freeze, or narrow a sound | contributes to aggregated point value |
| right point | right-hand select, grab, freeze, or narrow a sound | contributes to aggregated point value |
| left open palm | expand left-hand control, `CC27` high / about `10V` | more accent / brightness |
| left fist | contract left-hand control, `CC27` low / `0V` | lower openness |
| right open palm | expand right-hand control, `CC29` high / about `10V` | more accent / brightness |
| right fist | contract right-hand control, `CC29` low / `0V` | lower openness |
| pulse | point onset or sudden movement, `CC23` event | WaterfallA stream recomposition |
| swipe | fast horizontal movement, `CC30` event | toggle WaterfallA / WaterfallB |

## Tuning

Useful bridge options:

```bash
python hand_gesture_bridge.py --speed-scale 1.8 --smoothing 0.14 --pulse-threshold 0.35 --swipe-threshold 1.85
```

For smoother Unity hand visualization, keep `--hand-send-rate 60`. The WaterfallA control stream can stay at the default `--unity-send-rate 25`.

If the system is too nervous:

- increase `--smoothing`
- increase `--pulse-threshold`
- increase `--swipe-threshold`
- increase `--swipe-cooldown`
- lower VCV modulation depths

If it is too quiet:

- decrease `--speed-scale`
- decrease `--pulse-threshold`
- decrease `--swipe-threshold`
- lower `WaterfallBodyControlReceiver.responseSpeed` less cautiously only after the bridge feels stable

## Next Step

After v1 is stable, the next useful Unity addition is a small scene object that owns both `WaterfallBodyControlReceiver` and `HandLandmarkVisualizer`, with tuned defaults for Sonic Arts. That keeps the thesis scene reusable while making the performance setup faster.
