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
- WaterfallA uses DataWaterfallVertical as a bidirectional collective body signal field with offscreen-generated labels that keep their token while visible.
- WaterfallA parameters should later become dynamically controllable, especially density, speed, bidirectional stream ratio, freeze/recompose events, glitch, accent, and label visibility.
- WaterfallA is the next target for VCV / rhythm control.

## Current VCV Direction
- VCV is currently a highly self-generating IDM patch.
- An older TouchDesigner workflow sent processed MediaPipe data to VCV, but direct body control made the music feel too dense.
- Next development should not modify run_mediapipe.py or aggregator.py core Unity communication.
- Preferred next step is a separate body_control_bridge.py sidecar.
- First bridge version should listen to the existing collective mprot stream on 127.0.0.1:53000 and send smoothed 0-1 OSC/UDP signals to VCV on a new 54000+ port.
- Candidate signals: /body/energy, /body/stillness, /body/asymmetry, /body/height, /body/upper, /body/lower, /body/pulse, /body/presence.
- Do not send raw quaternions directly to many VCV knobs; keep the control surface small, smoothed, and musically legible.
- Conceptual route: audience body -> body_control_bridge.py -> VCV; VCV rhythm/state -> Unity WaterfallA.

## Runtime Separation Notes
- Fragment / UPose / avatar / aggregator are the main body pipeline and should remain isolated from WaterfallB audio work.
- Waterfall output modes should keep BackgroundRoot active and fragment/avatar/UDP roots inactive, avoiding UPose UDP port conflicts.
- MediaPipe / UPose ports are 52733-52736, 52833-52836, and 53000.
- Future WaterfallA / VCV communication should use a separate port range, preferably 54000+.

## Coding Rules for AI
- Make minimal, high-confidence changes
- Preserve existing public fields and Unity Inspector references
- Do not rename serialized fields unless explicitly requested
- Do not rewrite the whole architecture without asking
- Explain changed files after editing
- Avoid assuming Unity scene hierarchy unless visible in code
- For Unity, remember that some references are assigned in the Inspector and may not appear in code
