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

Mac Studio M1 Max deployment status: environment setup and camera index mapping verified. Python / MediaPipe / UPose / UDP flow has been tested successfully on the Mac Studio.

TODO: document exact Python version, package versions, and Unity Editor version. The `mediapipe` conda environment is verified on the Mac Studio, but an exported package list is not yet recorded in this repository.

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

## Windows One-Click Startup

Windows startup script:

```text
Unity/MotionCapture/mediapipe/start_pose_system_windows.bat
```

Before using it, confirm camera indexes:

```powershell
cd Unity/MotionCapture/mediapipe
python list_cameras_windows.py
```

Camera indexes are machine-dependent. The script currently defines:

```bat
set CAM_P1=0
set CAM_P2=1
set CAM_P3=2
set CAM_P4=3
```

Edit those values in `start_pose_system_windows.bat` if `list_cameras_windows.py` reports a different mapping.

Run the launcher from File Explorer or from terminal:

```powershell
cd Unity/MotionCapture/mediapipe
.\start_pose_system_windows.bat
```

If Windows Terminal `wt` is available, the launcher opens one Windows Terminal window with tabs:

- `Aggregator`
- `P1 cam%CAM_P1%`
- `P2 cam%CAM_P2%`
- `P3 cam%CAM_P3%`
- `P4 cam%CAM_P4%`

Each tab runs `conda activate mediapipe` before launching Python. The launcher also sets `PYTHONPATH` so `run_mediapipe.py` can import the local `upose` package.

If `wt` is not available or fails to launch, the script falls back to separate `cmd` windows. The fallback windows stay open so logs remain visible.

Windows stopping:

- In Windows Terminal mode, stop each tab with `Ctrl+C`, or close the Windows Terminal window.
- In fallback mode, stop each `cmd` window with `Ctrl+C`, or close the windows.

Expected port mapping:

| Stream | Camera index variable | Unity solo port | Aggregator input port |
| --- | --- | --- | --- |
| P1 | `CAM_P1` | `52733` | `52833` |
| P2 | `CAM_P2` | `52734` | `52834` |
| P3 | `CAM_P3` | `52735` | `52835` |
| P4 | `CAM_P4` | `52736` | `52836` |

Aggregator output:

| Source | Destination |
| --- | --- |
| `aggregator.py` fused collective body | Unity port `53000` |

Code check result:

- `run_mediapipe.py` accepts `camera_id`, `unity_port`, then `agg_port`.
- `run_mediapipe.py` sends each `mprot` packet to both `unity_port` and `agg_port`.
- `aggregator.py` listens on `52833-52836`.
- `aggregator.py` outputs to `127.0.0.1:53000`.
- This matches the confirmed DanceScene port design.

## macOS Startup

macOS startup script:

```text
Unity/MotionCapture/mediapipe/start_pose_system_mac.sh
```

Before first use, give it execute permission:

```bash
cd Unity/MotionCapture/mediapipe
chmod +x start_pose_system_mac.sh
```

Mac Studio M1 Max hardware test status:

- Environment setup verified.
- Python / MediaPipe / UPose / UDP flow verified.
- `start_pose_system_mac.sh` is the current Mac Studio launcher baseline.
- Camera index mapping verified on the current Mac Studio setup:
  - `CAM_P1=0`
  - `CAM_P2=1`
  - `CAM_P3=2`
  - `CAM_P4=3`

Before using it on a changed Mac Studio hardware setup, confirm camera indexes:

```bash
cd Unity/MotionCapture/mediapipe
python list_cameras_mac.py
```

Camera indexes are machine-dependent. The script currently defines the Mac Studio verified default mapping:

```bash
CAM_P1=0
CAM_P2=1
CAM_P3=2
CAM_P4=3
```

Edit those values in `start_pose_system_mac.sh` if `list_cameras_mac.py` reports a different mapping. If the USB hub, camera ports, or physical camera order changes, or if macOS changes device ordering after reboot, rerun `python list_cameras_mac.py`.

Run the launcher:

```bash
cd Unity/MotionCapture/mediapipe
./start_pose_system_mac.sh
```

The macOS launcher first tries to use Terminal.app tabs through `osascript` / AppleScript. If tab launch succeeds, one Terminal window opens with tabs for:

- `aggregator.py`
- `run_mediapipe.py "$CAM_P1" 52733 52833`
- `run_mediapipe.py "$CAM_P2" 52734 52834`
- `run_mediapipe.py "$CAM_P3" 52735 52835`
- `run_mediapipe.py "$CAM_P4" 52736 52836`

Each tab activates `CONDA_ENV=mediapipe` and sets `PYTHONPATH=<script-dir>/../upose` before launching Python.

If Terminal.app tab automation fails, the script falls back to background processes in the current terminal. In fallback mode, it writes logs to:

```text
Unity/MotionCapture/mediapipe/logs/
```

In fallback mode, it writes launched process IDs to:

```text
Unity/MotionCapture/mediapipe/logs/pose_system_pids.txt
```

Expected port mapping:

| Stream | Camera index variable | Unity solo port | Aggregator input port |
| --- | --- | --- | --- |
| P1 | `CAM_P1` | `52733` | `52833` |
| P2 | `CAM_P2` | `52734` | `52834` |
| P3 | `CAM_P3` | `52735` | `52835` |
| P4 | `CAM_P4` | `52736` | `52836` |

The script sets:

```bash
CONDA_ENV=mediapipe
PYTHONPATH=<script-dir>/../upose
```

`PYTHONPATH` is required so `run_mediapipe.py` can import the local `upose` package.

macOS stopping:

- In Terminal.app tab mode, stop each tab with `Ctrl+C`, or close the Terminal window.
- In fallback background/logs mode, press `Ctrl+C` in the launcher terminal to stop all launched processes.
- In fallback mode, you can also stop by PID:

```bash
while read -r pid; do kill "$pid" 2>/dev/null; done < logs/pose_system_pids.txt
```

- If needed, force stop by command pattern:

```bash
pkill -f run_mediapipe.py
pkill -f aggregator.py
```

If Terminal.app asks for permission to control the computer, allow Terminal / osascript accessibility automation, then rerun the launcher. If permission is not granted, the script should fall back to background/logs mode.

## Mac Build / Output Modes

Author-confirmed project direction: the final installation/demo runtime is Mac / Mac Studio. Windows remains available for editing and local testing, but Windows build is not a current target.

`DanceScene.unity` includes `OutputModeManager.cs`. For built Mac apps, output mode can be selected with command line arguments:

```bash
./UPose.app/Contents/MacOS/UPose --mode Fragment
./UPose.app/Contents/MacOS/UPose --mode WaterfallA
./UPose.app/Contents/MacOS/UPose --mode WaterfallB
./UPose.app/Contents/MacOS/UPose --mode Full
```

Current modes:

| Mode | Notes |
| --- | --- |
| `Fragment` | Main fragment/avatar composition mode |
| `WaterfallA` | Background / waterfall output mode A |
| `WaterfallB` | Background / waterfall output mode B |
| `Full` | Enables all root objects |

`OutputModeManager` may set window resolution and enable/disable root GameObjects. If an expected layer is missing in a Mac build, first check the selected `--mode` and the `OutputModeManager` Inspector settings in `DanceScene`.

### Waterfall Visual Preview

Waterfall visuals currently live under `BackgroundRoot`.

For Unity Editor preview:

1. Select `OutputModeManager`.
2. Enable `Use Editor Preview Mode`.
3. Set `Editor Preview Mode` to `WaterfallA` or `WaterfallB`.
4. Enter Play Mode.
5. Confirm `BackgroundRoot` stays active.

If `BackgroundRoot` turns inactive in Play Mode, check `OutputModeManager.defaultMode` and `Use Editor Preview Mode`. In `Fragment` mode, `OutputModeManager` intentionally disables `BackgroundRoot`.

Current waterfall mode expectations:

| Mode | Resolution | Visual mode | Notes |
| --- | --- | --- | --- |
| `WaterfallA` | `1280 x 800` | `DataWaterfallVertical` | Vertical data streams made from small rectangular units |
| `WaterfallB` | `1024 x 768` | `TestPatternHorizontal` | Barcode-like horizontal test pattern / calibration lanes |

Current `WaterfallB` visual direction:

- Dense barcode-like rows.
- Mostly vertical stripe rectangles moving horizontally.
- Occasional long horizontal rectangles as signal overlays.
- White / gray is the main visual language.
- Cyan / green are small live-signal accents only.

Useful `WaterfallController` controls:

| Parameter | Use |
| --- | --- |
| `visualMode` | Switch between `TestPatternHorizontal` and `DataWaterfallVertical` |
| `speedMultiplier` | Global motion speed |
| `densityMultiplier` | Number of visible units / streams |
| `globalIntensity` | Overall brightness / alpha intensity |
| `accentProbability` | Chance of cyan / green accent units |
| `glitchProbability` | Chance of glitch / resample events |
| `pulseProbability` | Chance of pulse-related resets / blink behavior |
| `horizontalRowCount` | Number of barcode-like horizontal lanes |
| `horizontalUnitsPerRow` | Density per barcode lane |
| `horizontalBarcodeAlignment` | Keeps horizontal mode aligned to strict row / slot positions |
| `horizontalStripeProbability` | Ratio of vertical barcode stripe units |
| `horizontalStripeWidthRange` | Width range of barcode stripe units |
| `horizontalStripeHeightRange` | Height range of barcode stripe units |
| `horizontalLongBarProbability` | Chance of occasional X-axis long signal bars |
| `horizontalSpeedRange` | Per-unit speed range for horizontal mode |
| `horizontalUseSteppedMotion` | Optional stepped / test-signal movement; current default is continuous movement |

Future VCV / rhythm control is not connected yet. `WaterfallController` currently exposes these methods for future integration:

```csharp
SetIntensity(float value)
SetSpeedMultiplier(float value)
SetDensityMultiplier(float value)
TriggerPulse(float amount)
SetGlitchAmount(float value)
TriggerAccent(float amount)
```

Waterfall troubleshooting:

- If nothing appears, confirm `BackgroundRoot` is active in Play Mode.
- If `BackgroundRoot` is inactive, confirm the selected output mode is `WaterfallA` or `WaterfallB`.
- If the image feels too slow, first increase `speedMultiplier` or `horizontalSpeedRange`.
- If the horizontal mode looks too sparse, increase `densityMultiplier`, `horizontalUnitsPerRow`, or `horizontalUnitCount`.
- If green / cyan becomes too dominant, lower `accentProbability`.
- If horizontal movement looks too stepped, keep `horizontalUseSteppedMotion` disabled.

## Avatar Visual / Glitch Status

`ReadyPlayerAvatar.cs` includes runtime avatar material override, video glitch, and mesh glitch behavior.

Author-confirmed status:

- Avatar glitch Inspector settings have been personally tested by the author.
- Avatar glitch visual development is temporarily complete.
- Do not keep tuning these parameters unless explicitly requested.

Important references to preserve:

- `ReadyPlayerAvatar.overrideAvatarMaterials`
- `ReadyPlayerAvatar.avatarMaterial`
- `ReadyPlayerAvatar.enableAvatarGlitch`
- `ReadyPlayerAvatar.glitchTexture`
- `ReadyPlayerAvatar.suppressMeshGlitchInCollectiveSlotCameras`
- `Unity/UPose/Assets/Materials/MAT_Avatar_Unlit.mat`
- `Unity/UPose/Assets/Shaders/AvatarGlitchUnlit.shader`

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

Use this before running `start_pose_system_windows.bat`. If the camera order changes after unplugging/replugging devices, rerun the camera listing and update `CAM_P1` to `CAM_P4` in the launcher.

macOS helper:

```bash
python list_cameras_mac.py
```

Mac Studio M1 Max verified mapping:

| Stream | Camera index |
| --- | --- |
| `CAM_P1` | `0` |
| `CAM_P2` | `1` |
| `CAM_P3` | `2` |
| `CAM_P4` | `3` |

Use this before running `start_pose_system_mac.sh` if the hardware setup changes. If the USB hub, camera ports, camera order, or macOS device ordering changes after reboot, rerun the camera listing and update `CAM_P1` to `CAM_P4` in the launcher.

The existing `how to start.txt` mentions these example camera labels:

```text
0 = IR webcam
1 = Droidcam
2 = OBS virtual camera
```

Current Mac Studio test status: environment setup and camera index mapping verified.

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

For the Windows launcher, close the existing `Pose Aggregator 52833-52836 to 53000` terminal window before launching again.

### Camera index is wrong

Symptoms:

- The OpenCV preview shows the wrong camera.
- A preview window is black.
- `run_mediapipe.py` raises `Failed to open camera index`.

Try:

1. Run `python list_cameras_windows.py`.
2. Confirm which physical camera should be P1, P2, P3, and P4.
3. Edit `CAM_P1`, `CAM_P2`, `CAM_P3`, and `CAM_P4` in `start_pose_system_windows.bat`.
4. Relaunch the Windows startup script.

On macOS / Mac Studio:

1. Run `python list_cameras_mac.py`.
2. Confirm which physical camera should be P1, P2, P3, and P4.
3. Edit `CAM_P1`, `CAM_P2`, `CAM_P3`, and `CAM_P4` in `start_pose_system_mac.sh`.
4. Relaunch the macOS startup script.

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

If using `start_pose_system_windows.bat`, also check:

- Did the aggregator window print `[agg] listening on UDP 52833` through `[agg] listening on UDP 52836`?
- Did the P1-P4 windows print the expected `unity_port` and `agg_port` values?
- Are the OpenCV preview windows detecting pose landmarks?

If using `start_pose_system_mac.sh`, also check:

- In Terminal.app tab mode, did each tab start with the expected process title/command?
- In fallback mode, do the files in `logs/` show aggregator and P1-P4 startup logs?
- Did the P1-P4 logs print the expected `unity_port` and `agg_port` values?
- Are the OpenCV preview windows detecting pose landmarks?

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

Mac Studio M1 Max status: `conda activate mediapipe`, Python / MediaPipe / UPose import, and UDP flow have been verified. Remaining documentation task: record exact Python version and package versions, for example with `python --version` and `conda env export`.

### Conda activate fails

The launchers expect a conda environment named:

```text
mediapipe
```

Windows launcher:

```bat
set CONDA_ENV=mediapipe
```

macOS launcher:

```bash
CONDA_ENV=mediapipe
```

If activation fails:

- Confirm conda is installed and available in the terminal.
- Check environments with `conda env list`.
- If the environment name is different, update `CONDA_ENV` in the launcher.
- On macOS, if `conda` is not available in the shell, initialize conda for that shell first. The script needs `conda info --base` and `<conda-base>/etc/profile.d/conda.sh`.

### `upose` import fails

Symptom:

```text
ModuleNotFoundError: No module named 'upose'
```

Check:

- The launcher is being run from `Unity/MotionCapture/mediapipe`, or use the launcher so it can auto-locate its own folder.
- On macOS, `start_pose_system_mac.sh` sets `PYTHONPATH` to include `Unity/MotionCapture/upose`.
- If running manually, set `PYTHONPATH` before launching:

```bash
cd Unity/MotionCapture/mediapipe
export PYTHONPATH="$(cd ../upose && pwd):${PYTHONPATH}"
python run_mediapipe.py 0 52733 52833
```

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
- Exact final projection mapping workflow. Current final runtime platform is Mac / Mac Studio; development assumes Unity single-window output first, with MadMapper or Resolume mapping later.
- Optional screenshots or copied logs from successful Play Mode verification.
- Port diagram for solo streams vs collective stream.
- Explanation of how solo P1 to P4 slots relate to camera streams, if they are currently connected.
- Calibration procedure for gallery installation.
