# AI Context for Thesis Project

## Project Identity
This is an MFA thesis installation project at Pratt Institute, Department of Digital Arts.

The project explores a "collective body" generated from multiple audience members' movements in a dark gallery space.

## Core Concept
The installation uses camera-based motion capture to collect audience movement.
Multiple bodies are merged into a composite avatar.
The projected image is fragmented into moving camera views of the avatar's body.
The goal is not accurate individual representation, but a posthuman collective presence.

## Technical Stack
- Unity for visual system and projection output
- Python + MediaPipe for pose tracking
- UDP for pose data transfer
- aggregator.py combines multiple camera streams
- Unity receives aggregated pose data and drives an avatar
- FragmentSlot and FragmentController manage moving render-texture fragments
- OutputModeManager selects Fragment / WaterfallA / WaterfallB / Full output modes
- WaterfallController manages procedural WaterfallA / WaterfallB visuals under BackgroundRoot
- WaterfallAudioReactiveController lets WaterfallB react to system / VCV audio routed through BlackHole 2ch
- ReadyPlayerAvatar keeps normal tracked motion as direct pass-through, but includes a narrow lost-tracking fallback that detects all-identity / invalid mprot rotations and blends the avatar to the last valid tracked pose. It avoids using the raw rest / T-pose by default because this avatar's rest orientation can appear supine.
- Projection mapping may be handled later with MadMapper or Resolume

## Current Known System Flow
Camera(s)
→ Python MediaPipe capture scripts
→ UDP per-camera ports
→ aggregator.py
→ UDP to Unity
→ UPose / avatar rig
→ fragment cameras / render textures
→ Unity output
→ projector mapping

## Current Waterfall Status
- WaterfallB is visually complete enough to treat as a formal preset.
- WaterfallB uses TestPatternHorizontal: three barcode-like horizontal lanes, white/gray rectangles, sparse cyan/green accents, and fixed-baseline data labels.
- WaterfallB does not communicate with VCV directly.
- WaterfallB can react to system audio by listening to BlackHole 2ch through Unity Microphone input.
- Current WaterfallB audio tuning is environment-sensitive; exhibition setup may require adjusting rmsFloor, rmsCeil, smoothing, lanePaddingMin, and lanePaddingMax.
- WaterfallController and WaterfallAudioReactiveController can save Play Mode tuning to StreamingAssets JSON files.
- WaterfallA is visually usable as the current preset, but not final.
- WaterfallA uses DataWaterfallVertical as a MIDI / CC control-field preset with offscreen-generated labels that keep their token while visible.
- WaterfallA labels now use MIDI-related tokens such as MIDI_CC, IAC_BUS1, CH01, CC_20_ENR, CC_23_PLS, CC_24_ASY, MIDI_MAP, VCV_CORE, CV_OUT, NOTE_ON, and MAP_LEARN.
- CC-related WaterfallA labels append the MIDI value captured at label generation time, for example CC_23_PLS:127.
- WaterfallA parameters should become dynamically controllable from the body bridge, especially density, speed, bidirectional stream ratio, freeze/recompose events, glitch, accent, and label visibility.
- WaterfallA is now the next visual-response target for direct bridge-to-Unity control.

## Current VCV Direction
- VCV is currently a highly self-generating IDM patch.
- An older TouchDesigner workflow sent processed MediaPipe data to VCV, but direct body control made the music feel too dense.
- Next development should not modify run_mediapipe.py or aggregator.py core Unity communication.
- First body-to-VCV sidecar is `Unity/MotionCapture/mediapipe/body_control_bridge.py`.
- Because Unity binds UDP 53000 exclusively, `aggregator.py` now preserves its Unity output on 127.0.0.1:53000 and mirrors the same collective mprot packets to 127.0.0.1:53100 for the bridge.
- `body_control_bridge.py` listens on 53100 by default and can send smoothed body controls to VCV through OSC and/or MIDI CC.
- Python -> VCV communication is now usable as version 1.0. Current VCV mapping choices can wait until installation testing.
- Next direction: skip VCV -> Unity for WaterfallA. Let `body_control_bridge.py` send the same high-level body controls directly to Unity on a separate UDP port, while MIDI CC continues to control VCV.
- Candidate signals: /body/energy, /body/stillness, /body/asymmetry, /body/height, /body/upper, /body/lower, /body/pulse, /body/presence.
- Do not send raw quaternions directly to many VCV knobs; keep the control surface small, smoothed, and musically legible.
- Conceptual route: audience body -> body_control_bridge.py -> VCV MIDI CC for sound, and body_control_bridge.py -> Unity WaterfallA UDP for visuals.

## Runtime Separation Notes
- Fragment / UPose / avatar / aggregator are the main body pipeline and should remain isolated from WaterfallB audio work.
- Waterfall output modes should keep BackgroundRoot active and fragment/avatar/UDP roots inactive, avoiding UPose UDP port conflicts.
- MediaPipe / UPose ports are 52733-52736, 52833-52836, and 53000.
- Future WaterfallA communication should use a separate port range, preferably 55000+ for Unity visual controls.

## Current Pre-Exhibition Status
- Core pre-exhibition development is roughly complete. The remaining work before the installation is Mac Studio pull, dependency check, Unity compile/build, and full hardware validation.
- Recommended build timing: build on the Mac Studio after pulling the latest commit, not on the development machine the night before.
- `start_pose_system_mac.sh` is the canonical macOS launcher. `start_pose_system_mac.command` is only a Finder double-click wrapper that delegates to the `.sh` file.
- The launcher defaults to background/logs mode and starts aggregator, body bridge, and four MediaPipe capture processes. It checks MIDI dependencies before launching MIDI bridge mode.

## Coding Rules for AI
- Make minimal, high-confidence changes
- Preserve existing public fields and Unity Inspector references
- Do not rename serialized fields unless explicitly requested
- Do not rewrite the whole architecture without asking
- Explain changed files after editing
- Avoid assuming Unity scene hierarchy unless visible in code
- For Unity, remember that some references are assigned in the Inspector and may not appear in code
