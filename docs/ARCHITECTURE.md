# Architecture

本文档描述当前 thesis project 的技术结构。内容基于当前 repository 中可见的文件和代码整理；无法从代码确认的部分会标记为 TODO。

## Project Goal

这是 Pratt Institute Department of Digital Arts 的 MFA thesis installation。项目目标是在黑暗 gallery space 中，通过 camera-based motion capture 捕捉多个 audience members 的动作，并把这些身体输入合成为一个 collective body / composite avatar。

视觉目标不是准确再现某一个人的身体，而是生成一种 posthuman collective presence。Unity 输出会把 avatar 的身体切成多个 moving camera views / render-texture fragments，使身体影像以局部、漂移、重组的方式出现。

## High-Level Pipeline

```text
Camera(s)
-> Python MediaPipe capture
-> Python UPose rotation extraction
-> UDP mprot packets
-> aggregator.py
-> UDP 127.0.0.1:53000
-> Unity UPose.cs / ServerUDP.cs
-> ReadyPlayerAvatar.cs drives avatar bones
-> BoneTrackingCamera.cs follows selected bones
-> FragmentSlot.cs renders cameras into RenderTextures
-> FragmentController.cs arranges solo / collective fragments
-> Unity projection output
-> Development assumption: Unity single-window output
-> Later: external projection mapping with MadMapper or Resolume
```

## Repository Areas

- `docs/`: project documentation.
- `Unity/MotionCapture/mediapipe/`: Python MediaPipe capture scripts, UDP sender, aggregator.
- `Unity/MotionCapture/upose/`: Python UPose package used to compute pose rotations.
- `Unity/UPose/`: Unity project.
- `Unity/UPose/Assets/Scripts/`: Unity runtime scripts for UDP receiving, avatar driving, fragment rendering, and related interaction systems.
- `Unity/UPose/Assets/Scenes/`: Unity scenes, including active scene `DanceScene.unity` and `TestScene.unity`.
- `Unity/UPose/Assets/StreamingAssets/`: GLB avatar assets loaded by `ReadyPlayerAvatar.cs`.
- `VCV/`: VCV project will run alongside the Unity project. It may later send data to Unity through UDP.
- `TD/`: TouchDesigner files are currently considered leftovers unless reactivated later.

## Python Capture Layer

The main capture script is:

- `Unity/MotionCapture/mediapipe/run_mediapipe.py`

It does the following:

1. Reads command line arguments:
   - `camera_id`, default `0`
   - `unity_port`, current code default `52733`
   - `agg_port`, current code default `52833`
2. Opens the camera through OpenCV:
   - Windows uses `cv2.CAP_DSHOW`
   - macOS uses `cv2.CAP_AVFOUNDATION`
   - other platforms use the default OpenCV backend
3. Runs `mediapipe.solutions.pose.Pose`.
4. Sends each detected frame into Python `UPose`.
5. Calls `computeRotations()`.
6. Extracts local quaternion rotations for 10 joints.
7. Builds an `mprot` text packet.
8. Sends the packet by UDP both to a Unity port and to an aggregator port.

Unity Inspector-confirmed port design:

- `52733-52736` are direct Unity solo streams for P1-P4.
- `52833-52836` are received by `aggregator.py` for collective body calculation.
- `aggregator.py` should send the collective body stream to Unity port `53000`.

The script displays an OpenCV preview window named like `MediaPipe Pose cam0`. Pressing `Esc` exits the loop.

Windows startup helper:

- `Unity/MotionCapture/mediapipe/start_pose_system_windows.bat`

This launcher starts `aggregator.py` and four `run_mediapipe.py` processes in separate terminal windows. It preserves the confirmed port mapping:

| Stream | Direct Unity solo port | Aggregator input port |
| --- | --- | --- |
| P1 | `52733` | `52833` |
| P2 | `52734` | `52834` |
| P3 | `52735` | `52835` |
| P4 | `52736` | `52836` |

Camera indexes are machine-dependent. Confirm them with `list_cameras_windows.py` before relying on the launcher defaults.

## Python UPose Rotation Extraction

`run_mediapipe.py` imports:

- `from upose import UPose`

The `UPose` object is created as:

```python
pose_tracker = UPose(source="mediapipe", flipped=True)
```

The script currently extracts these 10 rotations:

| mprot index | Python getter | Unity landmark |
| --- | --- | --- |
| `0` | `getPelvisRotation()` | `Landmark.PELVIS` |
| `1` | `getTorsoRotation()` | `Landmark.SHOULDER_CENTER` |
| `2` | `getLeftShoulderRotation()` | `Landmark.LEFT_SHOULDER` |
| `3` | `getRightShoulderRotation()` | `Landmark.RIGHT_SHOULDER` |
| `4` | `getLeftElbowRotation()` | `Landmark.LEFT_ELBOW` |
| `5` | `getRightElbowRotation()` | `Landmark.RIGHT_ELBOW` |
| `6` | `getLeftHipRotation()` | `Landmark.LEFT_HIP` |
| `7` | `getRightHipRotation()` | `Landmark.RIGHT_HIP` |
| `8` | `getLeftKneeRotation()` | `Landmark.LEFT_KNEE` |
| `9` | `getRightKneeRotation()` | `Landmark.RIGHT_KNEE` |

TODO: document the exact math inside `Unity/MotionCapture/upose/upose/upose.py` if this becomes important for thesis technical writing.

## UDP Protocol

The active rotation protocol is `mprot`.

Packet shape:

```text
mprot
0|qx|qy|qz|qw|visibility
1|qx|qy|qz|qw|visibility
...
9|qx|qy|qz|qw|visibility
```

Notes:

- Quaternion values are sent as `x, y, z, w`.
- Visibility is included as the sixth field.
- `clientUDP.py` appends `<EOM>` to sent messages.
- `UPose.cs` splits received messages by newline and parses the `mprot` lines.
- `aggregator.py` accepts only packets whose first line is exactly `mprot`.

There is also support in `UPose.cs` for `mpxyz`, which appears to be a position-based format. The current MediaPipe script reviewed here sends `mprot`.

## Aggregator Layer

The aggregator script is:

- `Unity/MotionCapture/mediapipe/aggregator.py`

Unity Inspector-confirmed port design:

- `IN_PORTS` are `52833-52836` for camera streams used to calculate the collective body.
- Aggregator output should be `127.0.0.1:53000`.
- Unity separately receives solo streams on `52733-52736`.

Current observed config in code:

```python
IN_PORTS = [52833, 52834, 52835, 52836]
OUT_ADDR = ("127.0.0.1", 53000)
STALE_SEC = 0.5
TARGET_HZ = 30
JOINT_COUNT = 10
```

Behavior:

1. Opens one non-blocking UDP socket per input port.
2. Receives `mprot` packets from multiple camera streams.
3. Keeps the latest packet per port.
4. Treats a stream as active if its last packet is not older than `STALE_SEC`.
5. For each joint index from `0` to `9`, averages active quaternions.
6. Handles quaternion sign ambiguity with dot-product comparison before averaging.
7. Averages visibility values.
8. Sends a fused `mprot` packet to `127.0.0.1:53000` at up to `TARGET_HZ`.

This is the current technical location where multiple bodies / camera streams become one collective pose.

Author-confirmed current direction: the collective body continues to use the quaternion average implemented in `aggregator.py`. Weighted, selective, or fragmented body-part assignment may be explored later, but it is not a current development target.

## Unity Receiving Layer

The primary Unity receiving scripts are:

- `Unity/UPose/Assets/Scripts/UPose.cs`
- `Unity/UPose/Assets/Scripts/ServerUDP.cs`
- `Unity/UPose/Assets/Scripts/MotionTrackingPose.cs`

`UPose.cs` defaults:

```csharp
public string host = "127.0.0.1";
public int port = 53000;
```

Author-confirmed Unity receiving design:

- Unity must listen to `53000` as the collective body stream.
- Unity must also listen to `52733-52736` as 4 solo streams.

Unity Inspector-confirmed `MotionTracking / UPose` objects:

| Object | Port | Role |
| --- | --- | --- |
| `MotionTracking_Collective` | `53000` | Collective body |
| `MotionTracking_P1` | `52733` | Solo P1 |
| `MotionTracking_P2` | `52734` | Solo P2 |
| `MotionTracking_P3` | `52735` | Solo P3 |
| `MotionTracking_P4` | `52736` | Solo P4 |

When `useCSV` is false, `UPose.cs` starts a thread and calls `Run()`. In `Run()`:

1. It creates `ServerUDP(host, port)`.
2. It calls `Connect()`.
3. It calls `StartListeningAsync()`.
4. It reads queued UDP messages.
5. It parses `mprot` packets into `body.rotations`.
6. It increments `frame_counter`.

`ServerUDP.cs` wraps UDP receiving with:

- a background thread
- a `ConcurrentQueue<string>`
- `HasMessage()`
- `GetMessage()`
- `Disconnect()`

`MotionTrackingPose.cs` defines the shared interface:

```csharp
public interface MotionTrackingPose
{
    public Quaternion GetRotation(Landmark i);
    public Quaternion GetRotation(Landmark i, int Delay);
    public long getFrameCounter();
}
```

This lets other components read pose data without depending directly on the full `UPose` implementation.

## CSV Playback Mode

`UPose.cs` includes a `useCSV` mode. When enabled, it loads rotation frames from `csvFilePath` and plays them at `csvFPS`.

The documented CSV shape in comments is:

```text
frame,
pelvis_x,pelvis_y,pelvis_z,pelvis_w,
torso_x,torso_y,torso_z,torso_w,
...
```

TODO: confirm which scene or workflow currently uses CSV playback, if any.

## Avatar Driving

The primary avatar script is:

- `Unity/UPose/Assets/Scripts/ReadyPlayerAvatar.cs`

It loads a GLB avatar through `GLTFast` and maps incoming `MotionTrackingPose` rotations onto a Mixamo-style rig.

Important behavior:

- It can use a local file from `Application.streamingAssetsPath`.
- It can also load avatar files from `https://digitalworlds.github.io/UPose/UPose/Assets/StreamingAssets/`.
- It finds bones by names such as `Hips`, `Spine`, `LeftArm`, `RightForeArm`, etc.
- In `Update()`, it applies rotations from `server.GetRotation(...)` to avatar bones.
- Arm rotations include fixed offsets like `Quaternion.Euler(0, 0, 90)`.

The script expects a `MotionTrackingPose` source. It can use an assigned `serverComponent`, or it searches parents and scene objects for `PoseMemory` or `UPose`.

Unity Inspector-confirmed avatar bindings:

| Avatar object | Motion tracking source |
| --- | --- |
| `Avatar_Collective` | `MotionTracking_Collective` |
| `Avatar_P1` | `MotionTracking_P1` |
| `Avatar_P2` | `MotionTracking_P2` |
| `Avatar_P3` | `MotionTracking_P3` |
| `Avatar_P4` | `MotionTracking_P4` |

## Pose Memory

The script:

- `Unity/UPose/Assets/Scripts/PoseMemory.cs`

stores recent pose rotations in circular buffers. It implements `MotionTrackingPose` and can return rotations with a frame delay through:

```csharp
GetRotation(Landmark landmark, int back_in_time)
```

This allows delayed avatars or time-offset body layers.

Author-confirmed current direction: the main thesis setup uses a single collective avatar. Multiple delayed avatars / `PoseMemory` should be treated as experimental or fallback mechanisms unless the active scene confirms they are currently in use.

## Fragment Rendering System

The fragment rendering system appears to be the main visual output layer.

Key scripts:

- `Unity/UPose/Assets/Scripts/FragmentController.cs`
- `Unity/UPose/Assets/Scripts/FragmentSlot.cs`
- `Unity/UPose/Assets/Scripts/FragmentProfile.cs`
- `Unity/UPose/Assets/Scripts/BoneTrackingCamera.cs`

### FragmentController.cs

`FragmentController.cs` manages two groups:

- `fixedSoloSlots`: fixed P1 to P4 corner slots.
- `randomCollectiveSlots`: moving collective fragments.

Unity Inspector-confirmed scene object:

- `FragmentController.cs` is attached to a GameObject named `FragmentController`.

Unity Inspector-confirmed `fixedSoloSlots`:

- `FragmentSlot_P1`
- `FragmentSlot_P2`
- `FragmentSlot_P3`
- `FragmentSlot_P4`

Unity Inspector-confirmed `randomCollectiveSlots`:

- `FragmentSlot_C1`
- `FragmentSlot_C2`
- `FragmentSlot_C3`
- `FragmentSlot_C4`
- `FragmentSlot_C5`
- `FragmentSlot_C6`

It controls:

- solo slot lifetime and refresh timing
- collective density
- collective spawn intervals
- collective region bounds
- Z layering
- overlap control
- avoiding solo corners
- Brownian motion for collective slots
- weighted bone selection
- camera profile generation per bone

The bone pools include names such as:

- `mixamorig:Hips`
- `mixamorig:Spine`
- `mixamorig:Spine2`
- `mixamorig:Head`
- `mixamorig:LeftArm`
- `mixamorig:RightArm`
- `mixamorig:LeftForeArm`
- `mixamorig:RightForeArm`
- `mixamorig:LeftHand`
- `mixamorig:RightHand`

### FragmentSlot.cs

`FragmentSlot.cs` owns one fragment display unit. It:

- auto-assigns a child `Camera`
- auto-assigns `BoneTrackingCamera`
- auto-assigns a screen `Renderer`
- creates a runtime `RenderTexture`
- assigns that render texture to a runtime material
- activates/deactivates the slot
- applies alpha fading
- applies screen shape scale
- tells `BoneTrackingCamera` which bone to follow

Unity Inspector-confirmed `FragmentSlot` prefab hierarchy:

```text
FragmentSlot
  Cam
  Screen
    Overlay
      Border_Top
      Border_Bottom
      Border_Left
      Border_Right
      FeedMarker
        FeedLabel
      TrackingBox_Frame
        TB_Top
        TB_Bottom
        TB_Left
        TB_Right
        TrackLabel
```

Unity Inspector-confirmed `FragmentSlot.cs` prefab-level references:

| Field | Reference |
| --- | --- |
| `Fragment Camera` | `Cam` (`Camera`) |
| `Tracking Camera` | `Cam` (`BoneTrackingCamera`) |
| `Screen Renderer` | `Screen` (`Mesh Renderer`) |
| `Overlay Root` | `Overlay` (`Transform`) |

Unity Inspector-confirmed render texture settings:

| Setting | Value |
| --- | --- |
| `Render Texture Width` | `512` |
| `Render Texture Height` | `512` |
| `Render Texture Depth` | `16` |
| `Render Texture Format` | `ARGB32` |
| `Screen Texture Property` | `_BaseMap` |

Unity Inspector-confirmed screen shape settings:

- `Normal Screen Scales` has 3 entries:
  - `(1, 1, 1)`
  - `(1.5, 1.5, 1)`
  - `(2, 2, 1)`
- `Distorted Screen Scales` is currently empty / 0 entries.

Important scene-instance note:

- The `FragmentSlot` prefab is dragged into the scene multiple times.
- Each scene instance has a manually modified `Slot Index`.
- `Cam`, `Screen`, `Overlay`, and related children are prefab child GameObjects.
- `BoneTrackingCamera` target/root differs by scene instance:
  - `FragmentSlot_P1-P4` target `Avatar_P1-P4`
  - `FragmentSlot_C1-C6` target `Avatar_Collective`
- Future AI/code changes must preserve prefab structure and scene instance overrides.

### BoneTrackingCamera.cs

`BoneTrackingCamera.cs` tracks a named bone under an avatar root.

Important default:

```csharp
public string avatarRootName = "Avatar_Collective";
```

It searches the scene for that object name, finds the requested bone recursively, smooths target position, and points the camera at the bone.

Author-confirmed: the active scene contains an object named exactly `Avatar_Collective`.

Unity Inspector-confirmed fragment camera targets:

- Each `FragmentSlot` has a child object named `Cam` with `BoneTrackingCamera`.
- Fixed `FragmentSlot_P1-P4` cameras target `Avatar_P1-P4` respectively.
- Random `FragmentSlot_C1-C6` cameras target `Avatar_Collective`.

## Play Mode Verification

Author-confirmed Play Mode verification results:

- UDP listeners start correctly.
- Unity listens on ports `52733`, `52734`, `52735`, `52736`, and `53000`.
- `MotionTracking_P1` connects to `UPose` on port `52733`.
- `MotionTracking_P2` connects to `UPose` on port `52734`.
- `MotionTracking_P3` connects to `UPose` on port `52735`.
- `MotionTracking_P4` connects to `UPose` on port `52736`.
- `MotionTracking_Collective` connects to `UPose` on port `53000`.
- `Avatar_P1`, `Avatar_P2`, `Avatar_P3`, `Avatar_P4`, and `Avatar_Collective` GLTF files load successfully.
- No blocking Console errors observed.

Known non-blocking warning:

- `SceneB.mp4` may show a WindowsMediaFoundation color primaries warning. This is currently treated as non-blocking unless visible color shift becomes a visual issue.

Previously verified visual behavior:

- `Avatar_Collective` and `Avatar_P1-P4` appear correctly.
- `FragmentSlot_P1-P4` are fixed at expected positions.
- `FragmentSlot_C1-C6` move randomly as expected.
- Each slot camera sees the intended avatar.

## Projection Output

Unity appears to generate the final visual output through cameras, render textures, fragment planes/screens, and scene composition.

Author-confirmed development assumption: during development, treat Unity output as a single window. Final projection mapping is not decided yet; the likely later workflow is to use MadMapper or Resolume.

## Build / Output Mode

Author-confirmed platform direction: the final project will run on Mac / Mac Studio. Windows remains useful for source editing and local development, but Windows build is not a current target.

The Unity project now includes `OutputModeManager.cs`, which selects an output mode at runtime from command line arguments:

```text
--mode Fragment
--mode WaterfallA
--mode WaterfallB
--mode Full
```

The current output modes are:

| Mode | Current role |
| --- | --- |
| `Fragment` | Main fragment/avatar composition mode |
| `WaterfallA` | Background / waterfall output mode A |
| `WaterfallB` | Background / waterfall output mode B |
| `Full` | Enables all root objects for full scene output |

`OutputModeManager` can set resolution through `Screen.SetResolution(...)` and can enable/disable scene root objects. Its current use should be understood as part of the Mac final/demo build workflow.

## Waterfall Visual System

Waterfall visuals are currently handled inside `BackgroundRoot`.

Author-confirmed current output-mode behavior:

- `Fragment` output: `BackgroundRoot` should be inactive.
- `WaterfallA` / `WaterfallB` output: `BackgroundRoot` should stay active, while fragment/avatar/UDP roots should be inactive.
- This separation avoids UDP port conflicts because waterfall windows do not need MediaPipe / UPose / avatar receivers.
- `SceneB` is not part of the current waterfall visual direction.

Current main script:

- `Unity/UPose/Assets/Scripts/WaterfallController.cs`

`WaterfallController` is independent from:

- `FragmentSlot.cs`
- `FragmentController.cs`
- `UPose.cs`
- `ReadyPlayerAvatar.cs`
- Python MediaPipe scripts
- `aggregator.py`

It uses runtime `Mesh` objects and runtime materials to draw procedural rectangular units. It does not create or destroy large numbers of GameObjects per frame.

Current visual modes:

| Mode | Current role | Visual language |
| --- | --- | --- |
| `TestPatternHorizontal` | Current `WaterfallB` direction | Horizontal barcode-like lanes, dense vertical stripes, short/long signal bars, outline rectangles, calibration / test-pattern language |
| `DataWaterfallVertical` | Current `WaterfallA` direction | Vertical streams made from small rectangular data units, falling/cascade motion, denser data-waterfall language |

Current preset mapping:

| Output mode | Resolution | `WaterfallController` preset | Default visual mode |
| --- | --- | --- | --- |
| `WaterfallA` | `1280 x 800` | `WaterfallA` | `DataWaterfallVertical` |
| `WaterfallB` | `1024 x 768` | `WaterfallB` | `TestPatternHorizontal` |

`WaterfallController` can read `OutputModeManager.CurrentMode` when `useOutputModeManagerPreset` is enabled, then select the matching preset.

Current `TestPatternHorizontal` / `WaterfallB` direction:

- `WaterfallB` is now a formal, showable preset.
- Mostly black background with white / light gray rectangular units.
- Three barcode-like horizontal lanes with top / center / bottom placement.
- Most elements are vertical stripe units moving horizontally.
- Occasional X-axis long rectangles are allowed as signal overlays.
- Green / cyan are only small accent signals, controlled by `accentProbability`, `accentColor`, and `secondaryAccentColor`.
- Movement is continuous by default; stepped movement can be re-enabled with `horizontalUseSteppedMotion`.
- `horizontalLanePadding` is audio-reactive and clamped to `0-2`.
- `horizontalLanePaddingResponseSpeed` is currently tuned to `20`.
- Top and bottom data labels are rendered on fixed horizontal baselines, not attached vertically to the moving barcode rows.

Important current Inspector parameters for `TestPatternHorizontal`:

- `horizontalRowCount`
- `horizontalUnitsPerRow`
- `horizontalBarcodeAlignment`
- `horizontalStripeProbability`
- `horizontalStripeWidthRange`
- `horizontalStripeHeightRange`
- `horizontalLanePadding`
- `horizontalLanePaddingResponseSpeed`
- `horizontalLongBarProbability`
- `horizontalLongWidthRange`
- `horizontalSpeedRange`
- `speedMultiplier`
- `densityMultiplier`
- `accentProbability`
- `glitchProbability`
- `pulseProbability`
- `horizontalShowDataLabels`
- `horizontalUseFixedLabelBaselines`
- `horizontalTopLabelBaselineY`
- `horizontalBottomLabelBaselineY`
- `horizontalUseAudioDataTokens`

`WaterfallB` has runtime text labels managed by `WaterfallController`:

- Labels are created from a TextMeshPro runtime pool; no scene label objects are required.
- Label tokens use a mixed 3-6 character vocabulary such as `CLK`, `SYNC`, `CV7E`, `0110`, `AMP72`, `PK91`, and `0x7E`.
- Optional realtime audio-data tokens are generated from `WaterfallAudioReactiveController`, including RMS / peak / high-ratio / transient-derived values.
- Default label color is white, with occasional cyan / green accent labels.
- Current label alignment keeps top and bottom label rows on fixed baselines while `x` position follows selected rectangle ends.

`WaterfallB` audio-reactive control is handled by:

- `Unity/UPose/Assets/Scripts/WaterfallAudioReactiveController.cs`

Current audio path:

```text
VCV / system audio
-> macOS Multi-Output Device
-> BlackHole 2ch
-> Unity Microphone input
-> WaterfallAudioReactiveController
-> WaterfallController

same Multi-Output Device
-> speakers / audio interface
```

Important behavior:

- `WaterfallB` does not receive VCV control messages directly.
- For installation testing, VCV and other system audio can affect `WaterfallB` by outputting to a macOS Multi-Output Device that includes `BlackHole 2ch`.
- `WaterfallAudioReactiveController.inputMode` defaults to `Microphone`.
- `microphoneDeviceName` defaults to `BlackHole 2ch`.
- `RMS` controls `SetLanePadding(...)` and `SetIntensity(...)`.
- Transient changes can call `TriggerPulse(...)`.
- High-frequency ratio can call `TriggerAccent(...)`.
- The current tuned test values are `rmsFloor = 0.06`, `rmsCeil = 0.07`, and `smoothing = 0`.

Runtime settings can be saved from Play Mode:

- `WaterfallController` context menu: `Save Current Settings`
- `WaterfallAudioReactiveController` context menu: `Save Current Settings`
- Saved files:
  - `Unity/UPose/Assets/StreamingAssets/WaterfallB_ControllerSettings.json`
  - `Unity/UPose/Assets/StreamingAssets/WaterfallB_AudioReactiveSettings.json`
- `loadSavedSettingsOnAwake` lets those JSON files override preset defaults on the next Play Mode run.

Current `DataWaterfallVertical` / `WaterfallA` direction:

- Many small white / gray rectangular segments arranged into streams / columns.
- Vertical falling / cascade motion.
- Small cyan / green accents may appear as live signal pulses.
- Intended as the next direct VCV-controlled data-waterfall surface.

Direct VCV / rhythm control is still intended for `WaterfallA`, not `WaterfallB`. `WaterfallController` currently keeps public control methods as the future integration surface:

```csharp
SetLanePadding(float value)
SetIntensity(float value)
SetSpeedMultiplier(float value)
SetDensityMultiplier(float value)
TriggerPulse(float amount)
SetGlitchAmount(float value)
TriggerAccent(float amount)
```

## Avatar Visual Style

`ReadyPlayerAvatar.cs` now includes an avatar material override and glitch visual system.

Current components:

- `Unity/UPose/Assets/Materials/MAT_Avatar_Unlit.mat`
- `Unity/UPose/Assets/Shaders/AvatarGlitchUnlit.shader`
- `ReadyPlayerAvatar.overrideAvatarMaterials`
- `ReadyPlayerAvatar.avatarMaterial`
- `ReadyPlayerAvatar.enableAvatarGlitch`
- video glitch parameters
- mesh glitch parameters

Author confirmation: avatar glitch Inspector settings have been personally tested by the author and this visual development thread is temporarily complete. Future AI changes should not continue tuning avatar glitch unless explicitly requested.

TODO: document the actual projection output path after checking the Unity scene and final hardware workflow:

- final camera name
- output resolution
- display index
- whether Unity outputs directly to projector
- whether Spout / NDI / Syphon / window capture is used
- whether MadMapper or Resolume is used

## Known Uncertainties

- Active Unity scene for thesis presentation: `DanceScene.unity`.
- Confirmed object hierarchy, Inspector assignments, and Play Mode behavior: motion tracking objects, avatar bindings, `FragmentController` arrays, `BoneTrackingCamera` targets, `FragmentSlot` prefab internals, UDP listeners, GLTF loading, fragment motion, and Console status are documented above.
- Author-confirmed final runtime platform: Mac / Mac Studio. Windows build is not a current target.
- Author-confirmed avatar glitch Inspector settings have been tested and are temporarily complete.
- TODO: exact final projection mapping tool and routing. Development assumes single-window Unity output first, then MadMapper or Resolume later.
- TODO: exact Python environment setup and dependency versions.
- Unity Inspector-confirmed port design: `52833-52836` are aggregator inputs for collective body; `52733-52736` are Unity solo streams; `53000` is Unity collective stream.
- VCV will run alongside Unity and may later send UDP data to Unity. TD is currently considered leftover.
