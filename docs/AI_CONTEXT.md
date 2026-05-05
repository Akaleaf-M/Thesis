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

## Coding Rules for AI
- Make minimal, high-confidence changes
- Preserve existing public fields and Unity Inspector references
- Do not rename serialized fields unless explicitly requested
- Do not rewrite the whole architecture without asking
- Explain changed files after editing
- Avoid assuming Unity scene hierarchy unless visible in code
- For Unity, remember that some references are assigned in the Inspector and may not appear in code