# upose

`upose` is a library that calculates joint rotations from mediapipe pose data.

## Installation

```bash
pip install upose
```

## Usage
```py
from upose import UPose

pose_tracker = UPose(source="mediapipe",flipped=True)
pose_tracker.newFrame(mediapipe_results)
pose_tracker.computeRotations()

# Get rotations:
pelvis_rotation = pose_tracker.getPelvisRotation()
pelvis_rotation["world"] #is world rotation
pelvis_rotation["local"] #is local rotation
pelvis_rotation["euler"] #is local rotation in Euler angles [x,y,z] that can be applied in zxy order to get the local rotation
pelvis_rotation["visibility"] #is visibility of the joint between [0,1], where 1 means visible joint

# Same for other rotations:
torso_rotation = pose_tracker.getTorsoRotation()
left_shoulder_rotation = pose_tracker.getLeftShoulderRotation()
right_shoulder_rotation = pose_tracker.getRightShoulderRotation()
left_elbow_rotation = pose_tracker.getLeftElbowRotation()
right_elbow_rotation = pose_tracker.getRightElbowRotation()
left_hip_rotation = pose_tracker.getLeftHipRotation()
right_hip_rotation = pose_tracker.getRightHipRotation()
left_knee_rotation = pose_tracker.getLeftKneeRotation()
right_knee_rotation = pose_tracker.getRightKneeRotation()

# You can also get major body angles:
print("Pelvis Angle:", pose_tracker.getPelvisAngle())
print("Left Elbow Angle:", pose_tracker.getLeftElbowAngle())
print("Right Elbow Angle:", pose_tracker.getRightElbowAngle())
print("Left Knee Angle:", pose_tracker.getLeftKneeAngle())
print("Right Knee Angle:", pose_tracker.getRightKneeAngle())
```
