# Python Startup

This document is the quick startup checklist for the Sonic Arts 1.0 hand gesture bridge.

## Working Directory

From the repository root:

```bash
cd Unity/MotionCapture/mediapipe
```

If using the Mac Studio conda setup:

```bash
conda activate mediapipe
```

## Dependencies

The bridge uses OpenCV, MediaPipe, and optional MIDI packages.

Install or refresh the MIDI dependencies if MIDI output fails:

```bash
python -m pip install mido python-rtmidi
```

Check MIDI output ports:

```bash
python hand_gesture_bridge.py --list-midi-ports
```

For VCV on macOS, keep IAC Driver enabled in Audio MIDI Setup. The bridge accepts `"IAC"` as a convenient alias for the IAC bus.

## Main Sonic Arts Command

Use this for the current Sonic Arts 1.0 performance setup:

```bash
python hand_gesture_bridge.py --camera-id 0 --output-mode both --midi-port-name "IAC" --unity-output --hand-output --hand-send-rate 60
```

This sends:

| Target | Protocol | Port / route |
| --- | --- | --- |
| Unity WaterfallA control | UDP `wctrl` | `127.0.0.1:55000` |
| Unity hand visualizer | UDP `hland` | `127.0.0.1:55010` |
| VCV Rack | OSC | `127.0.0.1:54000` |
| VCV Rack | MIDI CC | IAC MIDI output |

Press `Esc` in the OpenCV preview window to stop the bridge.

## Useful Variants

Run without the OpenCV preview:

```bash
python hand_gesture_bridge.py --camera-id 0 --output-mode both --midi-port-name "IAC" --unity-output --hand-output --hand-send-rate 60 --no-preview
```

Run Unity WaterfallA only, without VCV MIDI or OSC:

```bash
python hand_gesture_bridge.py --camera-id 0 --output-mode unity --unity-output --hand-output --hand-send-rate 60
```

Run VCV only, without Unity:

```bash
python hand_gesture_bridge.py --camera-id 0 --output-mode both --midi-port-name "IAC" --no-unity-output
```

If the camera index changes, try another `--camera-id` value.

## Gesture MIDI CC Map

| Gesture / signal | MIDI CC |
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
| `hand visibility trigger` | `31` |

`left open / fist` and `right open / fist` are continuous controls: fist is `0.0` / about `0V`, open palm is `1.0` / about `10V`.

`hand visibility trigger / CC31` is an event channel for VCV Run. It sends a short high trigger when visibility changes from no hands to one-or-more hands, and another short high trigger when visibility changes back to no hands. It does not stay high while hands remain visible.

## Unity Side

In the Unity Editor, use `DanceScene.unity` with `OutputModeManager` set to `SonicArts`.

For a built player:

```bash
./SonicArts.app/Contents/MacOS/UPose --mode SonicArts
```

If the hand visualization feels choppy only in macOS native fullscreen, disable macOS Game Mode and keep using the current project fullscreen settings.

## Tuning

Useful first tuning command:

```bash
python hand_gesture_bridge.py --camera-id 0 --output-mode both --midi-port-name "IAC" --unity-output --hand-output --hand-send-rate 60 --speed-scale 1.8 --smoothing 0.14 --pulse-threshold 0.35 --swipe-threshold 1.85
```

If gestures are too nervous, increase `--smoothing`, `--pulse-threshold`, `--swipe-threshold`, or `--swipe-cooldown`.

If gestures are too quiet, decrease `--pulse-threshold` or `--swipe-threshold`, and check VCV modulation depths.
