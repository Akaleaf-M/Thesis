# Runbook

本文档记录当前项目的实际启动步骤和排查方式。内容基于当前 repository 中可见的脚本整理；无法从代码确认的部分会标记为 TODO。

## Scope

本 runbook 覆盖以下流程：

```text
Python MediaPipe camera scripts
-> UDP camera streams
-> aggregator.py
-> Unity UPose receiver
-> avatar driving
-> fragment rendering
```

本文档不修改任何 source code、Unity scene、prefab、material 或 project settings。

## Prerequisites

需要的软件：

- Python environment with `cv2`, `mediapipe`, and local `upose` import available.
- Unity Editor that can open `Unity/UPose`.
- Camera devices available to OpenCV.
- Optional projection mapping software: final workflow is not decided yet. During development, assume Unity outputs a single window; later mapping will likely use MadMapper or Resolume.

TODO: document exact Python version, conda environment name, package versions, and Unity Editor version.

## Important Paths

- Python MediaPipe folder: `Unity/MotionCapture/mediapipe`
- Main capture script: `Unity/MotionCapture/mediapipe/run_mediapipe.py`
- Aggregator: `Unity/MotionCapture/mediapipe/aggregator.py`
- Camera listing helpers:
  - `Unity/MotionCapture/mediapipe/list_cameras_windows.py`
  - `Unity/MotionCapture/mediapipe/list_cameras_mac.py`
- Unity project: `Unity/UPose`
- Unity receiving script: `Unity/UPose/Assets/Scripts/UPose.cs`
- Avatar driver: `Unity/UPose/Assets/Scripts/ReadyPlayerAvatar.cs`
- Fragment controller: `Unity/UPose/Assets/Scripts/FragmentController.cs`

## Ports

Current known ports:

| Purpose | Port |
| --- | --- |
| Camera 1 direct Unity solo stream | `52733` |
| Camera 2 direct Unity solo stream | `52734` |
| Camera 3 direct Unity solo stream | `52735` |
| Camera 4 direct Unity solo stream | `52736` |
| Camera 1 to aggregator collective input | `52833` |
| Camera 2 to aggregator collective input | `52834` |
| Camera 3 to aggregator collective input | `52835` |
| Camera 4 to aggregator collective input | `52836` |
| Aggregator output to Unity collective receiver | `53000` |

Important:

- Unity Inspector-confirmed design: `aggregator.py` receives Python camera streams on `52833` to `52836`.
- `aggregator.py` sends to `127.0.0.1:53000`.
- Unity should listen on `53000` for the collective body.
- Unity should listen on `52733` to `52736` for 4 solo streams.
- `run_mediapipe.py` sends each camera packet both to `unity_port` and `agg_port`.

Unity Inspector-confirmed `MotionTracking / UPose` objects:

| Object | Port | Role |
| --- | --- | --- |
| `MotionTracking_Collective` | `53000` | Collective body |
| `MotionTracking_P1` | `52733` | Solo P1 |
| `MotionTracking_P2` | `52734` | Solo P2 |
| `MotionTracking_P3` | `52735` | Solo P3 |
| `MotionTracking_P4` | `52736` | Solo P4 |

## Recommended Startup Order

### 1. Open the Unity project

Open this folder in Unity:

```text
Unity/UPose
```

Open the active thesis scene:

- `Unity/UPose/Assets/Scenes/DanceScene.unity`

Known scene files:

- `Unity/UPose/Assets/Scenes/DanceScene.unity`
- `Unity/UPose/Assets/Scenes/TestScene.unity`

Before pressing Play, confirm in the Inspector:

- The active collective `UPose` component is listening on port `53000`.
- The Unity scene has receivers for solo streams on `52733-52736`.
- `useCSV` is disabled if receiving live UDP.
- `Avatar_Collective` uses `MotionTracking_Collective`.
- `Avatar_P1` uses `MotionTracking_P1`.
- `Avatar_P2` uses `MotionTracking_P2`.
- `Avatar_P3` uses `MotionTracking_P3`.
- `Avatar_P4` uses `MotionTracking_P4`.
- `FragmentController.cs` is attached to GameObject `FragmentController`.
- `FragmentController.fixedSoloSlots` has `FragmentSlot_P1`, `FragmentSlot_P2`, `FragmentSlot_P3`, `FragmentSlot_P4`.
- `FragmentController.randomCollectiveSlots` has `FragmentSlot_C1`, `FragmentSlot_C2`, `FragmentSlot_C3`, `FragmentSlot_C4`, `FragmentSlot_C5`, `FragmentSlot_C6`.
- Each `FragmentSlot` has child `Cam` with `BoneTrackingCamera`.
- Fixed `FragmentSlot_P1-P4` cameras target `Avatar_P1-P4` respectively.
- Random `FragmentSlot_C1-C6` cameras target `Avatar_Collective`.

Confirmed `FragmentSlot` prefab references:

- `Fragment Camera` -> `Cam` (`Camera`)
- `Tracking Camera` -> `Cam` (`BoneTrackingCamera`)
- `Screen Renderer` -> `Screen` (`Mesh Renderer`)
- `Overlay Root` -> `Overlay` (`Transform`)

Confirmed render texture settings:

- `Render Texture Width`: `512`
- `Render Texture Height`: `512`
- `Render Texture Depth`: `16`
- `Render Texture Format`: `ARGB32`
- `Screen Texture Property`: `_BaseMap`

Confirmed screen shape settings:

- `Normal Screen Scales`: `(1, 1, 1)`, `(1.5, 1.5, 1)`, `(2, 2, 1)`
- `Distorted Screen Scales`: empty / 0 entries

Important: the `FragmentSlot` prefab is dragged into the scene multiple times. Each scene instance has a manually modified `Slot Index`, and `BoneTrackingCamera` target/root differs by scene instance. Future changes must preserve prefab structure and scene instance overrides.

Play Mode verification has been completed by the author. UDP listeners start correctly, avatars load successfully, and no blocking Console errors were observed.

Known non-blocking warning:

- `SceneB.mp4` may show a WindowsMediaFoundation color primaries warning. Treat this as non-blocking unless visible color shift becomes a visual issue.

TODO: document exact scene object names and Inspector assignment locations.

### 2. Start the aggregator

Open a terminal in:

```text
Unity/MotionCapture/mediapipe
```

Run:

```powershell
python aggregator.py
```

Expected terminal output includes:

```text
[agg] listening on UDP 52833
[agg] listening on UDP 52834
[agg] listening on UDP 52835
[agg] listening on UDP 52836
[agg] sending to 127.0.0.1:53000
```

If any port bind fails, another process may already be using that UDP port.

### 3. Start Python MediaPipe capture scripts

Open one terminal per camera in:

```text
Unity/MotionCapture/mediapipe
```

For four cameras, the intended pattern is:

```powershell
python run_mediapipe.py 0 52733 52833
python run_mediapipe.py 1 52734 52834
python run_mediapipe.py 2 52735 52835
python run_mediapipe.py 3 52736 52836
```

Arguments:

```text
python run_mediapipe.py <camera_id> <unity_solo_port> <aggregator_collective_port>
```

Examples:

- Camera `0` sends direct Unity solo stream to `52733` and aggregator collective stream to `52833`.
- Camera `1` sends direct Unity solo stream to `52734` and aggregator collective stream to `52834`.

The script should print a line like:

```text
[run_mediapipe] cam=0, unity_port=52733, agg_port=52833
```

It should also open an OpenCV preview window named like:

```text
MediaPipe Pose cam0
```

Press `Esc` in the preview window to stop a capture script.

### 4. Press Play in Unity

After `aggregator.py` and at least one `run_mediapipe.py` process are running, press Play in Unity.

Expected Unity behavior:

- `ServerUDP.cs` logs that it connected/listens on UDP port `53000`.
- `UPose.cs` receives `mprot` packets.
- Solo Unity receivers receive `mprot` packets from `52733-52736`.
- `ReadyPlayerAvatar.cs` loads a GLB avatar.
- Avatar bones move according to incoming rotations.
- Fragment slots render bone-following camera views into screen fragments.

Author-confirmed Console / Play Mode results:

- UDP listeners start correctly.
- Unity listens on ports `52733`, `52734`, `52735`, `52736`, and `53000`.
- `MotionTracking_P1` connects to `UPose` on port `52733`.
- `MotionTracking_P2` connects to `UPose` on port `52734`.
- `MotionTracking_P3` connects to `UPose` on port `52735`.
- `MotionTracking_P4` connects to `UPose` on port `52736`.
- `MotionTracking_Collective` connects to `UPose` on port `53000`.
- `Avatar_P1`, `Avatar_P2`, `Avatar_P3`, `Avatar_P4`, and `Avatar_Collective` GLTF files load successfully.
- No blocking Console errors observed.

Author-confirmed visual behavior:

- `Avatar_Collective` and `Avatar_P1-P4` appear correctly.
- `FragmentSlot_P1-P4` are fixed at expected positions.
- `FragmentSlot_C1-C6` move randomly as expected.
- Each slot camera sees the intended avatar.

Known non-blocking warning:

- `SceneB.mp4` may show a WindowsMediaFoundation color primaries warning. Treat this as non-blocking unless visible color shift becomes a visual issue.

## Minimal Single-Camera Test

For a quick live test with one camera:

Terminal 1:

```powershell
cd Unity/MotionCapture/mediapipe
python aggregator.py
```

Terminal 2:

```powershell
cd Unity/MotionCapture/mediapipe
python run_mediapipe.py 0 52733 52833
```

Then press Play in Unity with `UPose.port = 53000`.

The aggregator will still output a fused `mprot` packet using the one active camera stream.


## Camera Listing

Windows helper:

```powershell
python list_cameras_windows.py
```

macOS helper:

```bash
python list_cameras_mac.py
```

The existing `how to start.txt` mentions these example camera labels:

```text
0 = IR webcam
1 = Droidcam
2 = OBS virtual camera
```

TODO: confirm the current machine's actual camera index mapping before installation.

## Port Checks on Windows

To see whether a UDP port is already in use:

```powershell
netstat -ano | findstr :53000
netstat -ano | findstr :52733
netstat -ano | findstr :52734
netstat -ano | findstr :52735
netstat -ano | findstr :52736
netstat -ano | findstr :52833
netstat -ano | findstr :52834
netstat -ano | findstr :52835
netstat -ano | findstr :52836
```

To identify a process by PID:

```powershell
tasklist /FI "PID eq <PID>"
```

Replace `<PID>` with the process id shown by `netstat`.

## Common Issues

### Aggregator cannot bind a port

Symptom:

- `aggregator.py` fails when binding `52833`, `52834`, `52835`, or `52836`.

Likely causes:

- Another aggregator is already running.
- Another process is using the same UDP port.

Try:

1. Close duplicate terminals.
2. Check ports with `netstat -ano | findstr :52833`.
3. Restart the aggregator.

### Unity does not move

Check:

- Is `aggregator.py` running?
- Is at least one `run_mediapipe.py` process sending to an aggregator input port `52833-52836`?
- Is Unity `UPose.port` set to `53000`?
- Are solo stream receivers listening on `52733-52736` if the solo views are expected to move?
- Is `useCSV` disabled on `UPose.cs` for live input?
- Does the Unity console show `[ServerUDP] Connected (listening) on UDP port 53000`?
- Does `ReadyPlayerAvatar.cs` have a valid `MotionTrackingPose` source?

TODO: add Unity Console screenshots or exact log examples from a successful run.

### Camera preview opens but no pose is detected

Check:

- The person is visible to the camera.
- Lighting is sufficient for MediaPipe.
- The correct camera index is being used.
- The OpenCV preview window shows the intended camera.
- The script prints FPS values after landmarks are detected.

### Python cannot import `mediapipe`, `cv2`, or `upose`

Likely cause:

- Wrong Python environment.
- Missing dependencies.
- Local `upose` package is not installed or not on `PYTHONPATH`.

TODO: document exact environment setup. The existing note says `conda activate mediapipe`, but the environment definition is not present in the repository.

### UDP sender reports receiver not listening

`clientUDP.py` may print:

```text
UDP send failed (receiver not listening yet).
```

This can happen if Unity or the aggregator is not yet listening. For UDP, this is not always fatal. Start the receiver process, then continue.

### Avatar loads but fragments are blank

Check:

- `FragmentSlot.cs` uses prefab child `Cam` as `Fragment Camera`.
- `FragmentSlot.cs` uses prefab child `Cam` as `Tracking Camera`.
- `FragmentSlot.cs` uses prefab child `Screen` as `Screen Renderer`.
- `FragmentSlot.cs` uses prefab child `Overlay` as `Overlay Root`.
- `BoneTrackingCamera.cs` can find `avatarRootName`.
- The avatar root name matches the scene object, default `Avatar_Collective`.
- The target bone name exists in the loaded avatar rig.
- The fragment camera has a valid target and is enabled while the slot is active.

If these references look correct but fragments are still blank, enter Play Mode and check Unity Console for runtime errors.

### Fragment cameras cannot find bones

Check:

- The loaded avatar uses `mixamorig:` bone names, or names compatible with `BoneTrackingCamera.cs`.
- `FragmentController.cs` bone lists match actual avatar bone names.
- `BoneTrackingCamera.avatarRootName` matches the scene object name.

### Text appears corrupted in some files

Several existing files contain mojibake / encoding corruption. This may affect comments and documentation display, but it does not necessarily affect runtime.

TODO: decide whether to normalize documentation encoding separately. Source code should not be rewritten only for comment cleanup unless requested.

## Shutdown Order

Recommended:

1. Stop Unity Play Mode.
2. Press `Esc` in each MediaPipe preview window.
3. Stop `aggregator.py` with `Ctrl+C`.
4. Close any remaining terminal processes.

## Notes for Future Documentation

Missing documentation that should be added later:

- Exact Unity scene setup and object hierarchy.
- Exact Python environment setup.
- Exact final projection mapping workflow. Development assumes Unity single-window output first, with MadMapper or Resolume mapping later.
- Optional screenshots or copied logs from successful Play Mode verification.
- Port diagram for solo streams vs collective stream.
- Explanation of how solo P1 to P4 slots relate to camera streams, if they are currently connected.
- Calibration procedure for gallery installation.
