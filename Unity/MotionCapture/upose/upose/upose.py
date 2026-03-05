# For more information and examples please visit:
# https://github.com/digitalworlds/UPose/
#
# Example use:
# pose_tracker = UPose(source="mediapipe",flipped=True)
# pose_tracker.newFrame(mediapipe_results)

# Get rotations:
# pelvis_rotation = pose_tracker.getPelvisRotation()
# pelvis_rotation["world"] is world rotation
# pelvis_rotation["local"] is local rotation
# pelvis_rotation["euler"] is local rotation in Euler angles [x,y,z] that can be applied in zxy order to get the local rotation
# pelvis_rotation["visibility"] is visibility of the joint between [0,1], where 1 means visible joint

# Same for other rotations:
# torso_rotation = pose_tracker.getTorsoRotation()
# left_shoulder_rotation = pose_tracker.getLeftShoulderRotation()
# right_shoulder_rotation = pose_tracker.getRightShoulderRotation()
# left_elbow_rotation = pose_tracker.getLeftElbowRotation()
# right_elbow_rotation = pose_tracker.getRightElbowRotation()
# left_hip_rotation = pose_tracker.getLeftHipRotation()
# right_hip_rotation = pose_tracker.getRightHipRotation()
# left_knee_rotation = pose_tracker.getLeftKneeRotation()
# right_knee_rotation = pose_tracker.getRightKneeRotation()

# You can also get major body angles:
# print("Pelvis Angle:", pose_tracker.getPelvisAngle())
# print("Left Elbow Angle:", pose_tracker.getLeftElbowAngle())
# print("Right Elbow Angle:", pose_tracker.getRightElbowAngle())
# print("Left Knee Angle:", pose_tracker.getLeftKneeAngle())
# print("Right Knee Angle:", pose_tracker.getRightKneeAngle())

# You can also get all Euler angles in a vector:
# print("Angle Vector: [" + ", ".join(f"{angle:.2f}" for angle in pose_tracker.getAngleVector()) + "]")
# 0:pelvis 1,2:torso, 3,4,5:left_shoulder, 6,7,8:right_shoulder, 9:left_elbow, 10:right_elbow, 11,12:left_hip, 13,14:right_hip, 15,16:left_knee, 17,18:right_knee 

# You can also get all Quaternions in a vector:
# print("Angle Vector: [" + ", ".join(f"{angle:.2f}" for angle in pose_tracker.getAngleVector(format="quaternions")) + "]")
# 0-3:pelvis 4,7:torso, 8-11:left_shoulder, 12-15:right_shoulder, 16-19:left_elbow, 20-23:right_elbow, 24-27:left_hip, 28-31:right_hip, 32-35:left_knee, 36-39:right_knee 


import numpy as np
import math
import mediapipe as mp
from scipy.spatial.transform import Rotation as R

class UPose:
    def __init__(self, source="mediapipe", flipped=False):
        if source.lower() != "mediapipe":
            raise ValueError("Only 'mediapipe' source is supported currently.")
        self.flipped = flipped
        self.source = source
        self.world_landmarks = None
        self.mp_pose = mp.solutions.pose
        LM = self.mp_pose.PoseLandmark
        if self.flipped:
            self.LEFT_SHOULDER=LM.RIGHT_SHOULDER.value
            self.RIGHT_SHOULDER=LM.LEFT_SHOULDER.value  
            self.LEFT_ELBOW=LM.RIGHT_ELBOW.value
            self.RIGHT_ELBOW=LM.LEFT_ELBOW.value 
            self.LEFT_WRIST=LM.RIGHT_WRIST.value    
            self.RIGHT_WRIST=LM.LEFT_WRIST.value 
            self.LEFT_HIP=LM.RIGHT_HIP.value  
            self.RIGHT_HIP=LM.LEFT_HIP.value
            self.LEFT_KNEE=LM.RIGHT_KNEE.value    
            self.RIGHT_KNEE=LM.LEFT_KNEE.value
            self.LEFT_ANKLE=LM.RIGHT_ANKLE.value    
            self.RIGHT_ANKLE=LM.LEFT_ANKLE.value   
        else:     
            self.LEFT_SHOULDER=LM.LEFT_SHOULDER.value
            self.RIGHT_SHOULDER=LM.RIGHT_SHOULDER.value
            self.LEFT_ELBOW=LM.LEFT_ELBOW.value
            self.RIGHT_ELBOW=LM.RIGHT_ELBOW.value 
            self.LEFT_WRIST=LM.LEFT_WRIST.value    
            self.RIGHT_WRIST=LM.RIGHT_WRIST.value 
            self.LEFT_HIP=LM.LEFT_HIP.value  
            self.RIGHT_HIP=LM.RIGHT_HIP.value
            self.LEFT_KNEE=LM.LEFT_KNEE.value    
            self.RIGHT_KNEE=LM.RIGHT_KNEE.value
            self.LEFT_ANKLE=LM.LEFT_ANKLE.value    
            self.RIGHT_ANKLE=LM.RIGHT_ANKLE.value
        self.resetFrame()

    def resetFrame(self):
        self.pelvis_rotation = None
        self.torso_rotation = None
        self.left_shoulder_rotation = None
        self.right_shoulder_rotation = None
        self.left_elbow_rotation = None
        self.right_elbow_rotation = None
        self.left_hip_rotation = None
        self.right_hip_rotation = None
        self.left_knee_rotation = None
        self.right_knee_rotation = None

    def newFrame(self, results):
        """Feed a new MediaPipe results object."""
        self.resetFrame()
        if results.pose_world_landmarks:
            self.world_landmarks = results.pose_world_landmarks
        else:
            self.world_landmarks = None

    def getPelvisRotation(self):
        """Returns signed Y-axis pelvis rotation in degrees, or None if unavailable."""

        if self.pelvis_rotation is not None:
            return self.pelvis_rotation
    
        if not self.world_landmarks:
            return None

        p1 = self.getLandmark(self.LEFT_HIP)
        p2 = self.getLandmark(self.RIGHT_HIP)

        direction = p2 - p1
        direction = direction / np.linalg.norm(direction)

        direction_xz = np.array([direction[0], 0, direction[2]])
        direction_xz = direction_xz / np.linalg.norm(direction_xz)

        angle_rad = math.atan2(direction_xz[2], direction_xz[0])
        angle_deg = math.degrees(angle_rad)

        euler = np.array([0, -angle_deg, 0])
        local = R.from_euler('zxy',[0, 0, -angle_deg],degrees=True)

        visibility = (self.getVisibility(self.LEFT_HIP)+self.getVisibility(self.RIGHT_HIP))/2

        self.pelvis_rotation = {
            "world": local,
            "local": local,
            "euler": euler,
            "visibility": visibility
        }

        return self.pelvis_rotation

    def getTorsoRotation(self):

        if self.torso_rotation is not None:
            return self.torso_rotation
        
        if not self.world_landmarks:
            return None

        # Get landmarks
        LM = self.mp_pose.PoseLandmark
        left_hip = self.getLandmark(self.LEFT_HIP)
        right_hip = self.getLandmark(self.RIGHT_HIP)
        left_shoulder = self.getLandmark(self.LEFT_SHOULDER)
        right_shoulder = self.getLandmark(self.RIGHT_SHOULDER)

        # Compute midpoints
        pelvis = (left_hip + right_hip) / 2
        shoulder_center = (left_shoulder + right_shoulder) / 2

        # Direction vector from pelvis to shoulders
        direction = shoulder_center - pelvis
        direction = direction / np.linalg.norm(direction)

        # Get pelvis rotation in degrees
        pelvis_rot = self.getPelvisRotation()
        if pelvis_rot is None:
            return None
        
        base_rotation = pelvis_rot["world"]

        # Inverse rotate direction vector into local space
        local_direction = base_rotation.inv().apply(direction)

        # Compute angles
        rot_z = math.asin(-local_direction[0]) * 180.0 / math.pi
        rot_x = math.atan2(local_direction[2], local_direction[1]) * 180.0 / math.pi

        euler = np.array([rot_x, 0, rot_z])
        local = R.from_euler('zxy',[rot_z, rot_x, 0], degrees=True)

        visibility = (self.getVisibility(self.LEFT_HIP)+self.getVisibility(self.RIGHT_HIP)+self.getVisibility(self.LEFT_SHOULDER)+self.getVisibility(self.RIGHT_SHOULDER))/4

        self.torso_rotation = {
            "world": base_rotation * local,
            "local": local,
            "euler": euler,
            "visibility": visibility
        }

        return self.torso_rotation

    def getLeftShoulderRotation(self):
   
        if self.left_shoulder_rotation is not None:
            return self.left_shoulder_rotation
        
        if not self.world_landmarks:
            return None

        # Get landmarks
        LM = self.mp_pose.PoseLandmark
        left_shoulder = self.getLandmark(self.LEFT_SHOULDER)
        left_elbow = self.getLandmark(self.LEFT_ELBOW)

        direction = left_elbow - left_shoulder
        direction = direction / np.linalg.norm(direction)

        # Get pelvis rotation in degrees
        torso_rot = self.getTorsoRotation()
        if torso_rot is None:
            return None
        
        base_rotation = torso_rot["world"]

        local_direction = (base_rotation * R.from_euler('z', 90, degrees=True)).inv().apply(direction)

        rot_z = math.asin(-local_direction[0]) * 180.0 / math.pi
        rot_x = math.atan2(local_direction[2], local_direction[1]) * 180.0 / math.pi
        

        euler = np.array([rot_x, 0, rot_z])
        local = R.from_euler('zxy', [rot_z, rot_x, 0], degrees=True)

        visibility = (self.getVisibility(self.LEFT_SHOULDER)+self.getVisibility(self.LEFT_ELBOW))/2

        self.left_shoulder_rotation = {
            "euler": euler,
            "local": R.from_euler('z', 90, degrees=True)*local,
            "world": base_rotation*R.from_euler('z', 90, degrees=True)*local,
            "visibility": visibility
        }

        return self.left_shoulder_rotation

    def getRightShoulderRotation(self):
   
        if self.right_shoulder_rotation is not None:
            return self.right_shoulder_rotation
        
        if not self.world_landmarks:
            return None

        # Get landmarks
        LM = self.mp_pose.PoseLandmark
        right_shoulder = self.getLandmark(self.RIGHT_SHOULDER)
        right_elbow = self.getLandmark(self.RIGHT_ELBOW)

        direction = right_elbow - right_shoulder
        direction = direction / np.linalg.norm(direction)

        # Get pelvis rotation in degrees
        torso_rot = self.getTorsoRotation()
        if torso_rot is None:
            return None
        
        base_rotation = torso_rot["world"]

        local_direction = (base_rotation * R.from_euler('z', -90, degrees=True)).inv().apply(direction)

        rot_z = math.asin(-local_direction[0]) * 180.0 / math.pi
        rot_x = math.atan2(local_direction[2], local_direction[1]) * 180.0 / math.pi


        euler = np.array([rot_x, 0, rot_z])
        local = R.from_euler('zxy', [rot_z, rot_x, 0], degrees=True)

        visibility = (self.getVisibility(self.RIGHT_SHOULDER)+self.getVisibility(self.RIGHT_ELBOW))/2

        self.right_shoulder_rotation = {
            "euler": euler,
            "local": R.from_euler('z', -90, degrees=True)*local,
            "world": base_rotation*R.from_euler('z', -90, degrees=True)*local,
            "visibility": visibility
        }

        return self.right_shoulder_rotation

    def getLeftElbowRotation(self):
        
        if self.left_elbow_rotation is not None:
            return self.left_elbow_rotation
        
        if not self.world_landmarks:
            return None

        LM = self.mp_pose.PoseLandmark
        elbow = self.getLandmark(self.LEFT_ELBOW)
        wrist = self.getLandmark(self.LEFT_WRIST)
        shoulder_rot = self.getLeftShoulderRotation()
       

        if shoulder_rot is None:
            return None

        direction = wrist - elbow
        direction = direction / np.linalg.norm(direction)

        base_rotation = shoulder_rot["world"]
        local_direction = base_rotation.inv().apply(direction)

        rot_z = -math.acos(local_direction[1]) * 180.0 / math.pi
        rot_y = math.atan2(-local_direction[2], local_direction[0]) * 180.0 / math.pi

        w = abs(rot_z)
        if w < 20:
            if w < 10:
                # Between 10–0: rotY becomes 0
                rot_y = 0
            else:
                # Between 20–10: linear interpolation from rotY to 0
                rot_y = rot_y * (w - 10) / 10

        #we do not use rot_y on the elbow
        euler = np.array([0, 0, rot_z])
        local = R.from_euler('zxy', [rot_z, 0, 0], degrees=True)

        visibility = (self.getVisibility(self.LEFT_ELBOW)+self.getVisibility(self.LEFT_WRIST))/2

        #we use the rot_y on the shoulder instead
        self.left_shoulder_rotation["local"]=self.left_shoulder_rotation["local"]*R.from_euler('zxy', [0, 0, rot_y], degrees=True)
        self.left_shoulder_rotation["world"]=self.left_shoulder_rotation["world"]*R.from_euler('zxy', [0, 0, rot_y], degrees=True)
        e = self.left_shoulder_rotation["local"].as_euler('zxy', degrees=True)
        self.left_shoulder_rotation["euler"]=np.array([e[1], e[2], e[0]])

        self.left_elbow_rotation= {
            "euler": euler,
            "local": local,
            "world": base_rotation * local,
            "visibility": visibility
        }

        return self.left_elbow_rotation

    def getRightElbowRotation(self):
        
        if self.right_elbow_rotation is not None:
            return self.right_elbow_rotation
        
        if not self.world_landmarks:
            return None

        LM = self.mp_pose.PoseLandmark
        elbow = self.getLandmark(self.RIGHT_ELBOW)
        wrist = self.getLandmark(self.RIGHT_WRIST)
        shoulder_rot = self.getRightShoulderRotation()

        if shoulder_rot is None:
            return None

        direction = wrist - elbow
        direction = direction / np.linalg.norm(direction)

        base_rotation = shoulder_rot["world"]
        local_direction = base_rotation.inv().apply(direction)

        rot_z = math.acos(local_direction[1]) * 180.0 / math.pi
        rot_y = math.atan2(local_direction[2], -local_direction[0]) * 180.0 / math.pi

        w = abs(rot_z)
        if w < 20:
            if w < 10:
                # Between 10–0: rotY becomes 0
                rot_y = 0
            else:
                # Between 20–10: linear interpolation from rotY to 0
                rot_y = rot_y * (w - 10) / 10

        #we do not use rot_y on the elbow
        euler = np.array([0, 0, rot_z])
        local = R.from_euler('zxy', [rot_z, 0, 0], degrees=True)

        visibility = (self.getVisibility(self.RIGHT_ELBOW)+self.getVisibility(self.RIGHT_WRIST))/2

        #we use the rot_y on the shoulder instead
        self.right_shoulder_rotation["local"]=self.right_shoulder_rotation["local"]*R.from_euler('zxy', [0, 0, rot_y], degrees=True);
        self.right_shoulder_rotation["world"]=self.right_shoulder_rotation["world"]*R.from_euler('zxy', [0, 0, rot_y], degrees=True);
        e = self.right_shoulder_rotation["local"].as_euler('zxy', degrees=True)
        self.right_shoulder_rotation["euler"]=np.array([e[1], e[2], e[0]])

        self.right_elbow_rotation= {
            "euler": euler,
            "local": local,
            "world": base_rotation * local,
            "visibility": visibility
        }

        return self.right_elbow_rotation

    def getLeftHipRotation(self):
        if self.left_hip_rotation is not None:
            return self.left_hip_rotation

        if not self.world_landmarks:
            return None

        LM = self.mp_pose.PoseLandmark
        left_hip = self.getLandmark(self.LEFT_HIP)
        left_knee = self.getLandmark(self.LEFT_KNEE)

        direction = left_knee - left_hip
        direction = direction / np.linalg.norm(direction)

        pelvis_rot = self.getPelvisRotation()
        if pelvis_rot is None:
            return None

        base_rotation = pelvis_rot["world"]
        local_direction = base_rotation.inv().apply(direction)

        rot_z = math.asin(local_direction[0]) * 180.0 / math.pi
        rot_x = math.atan2(-local_direction[2], -local_direction[1]) * 180.0 / math.pi

        if rot_x == -180:
            rot_x = 0

        euler = np.array([rot_x, 0, rot_z + 180])
        local = R.from_euler('zxy', [rot_z + 180, rot_x, 0], degrees=True)

        visibility = (self.getVisibility(self.LEFT_HIP)+self.getVisibility(self.LEFT_KNEE))/2

        self.left_hip_rotation = {
            "euler": euler,
            "local": local,
            "world": base_rotation * local,
            "visibility": visibility
        }

        return self.left_hip_rotation

    def getRightHipRotation(self):
        if self.right_hip_rotation is not None:
            return self.right_hip_rotation

        if not self.world_landmarks:
            return None

        LM = self.mp_pose.PoseLandmark
        right_hip = self.getLandmark(self.RIGHT_HIP)
        right_knee = self.getLandmark(self.RIGHT_KNEE)

        direction = right_knee - right_hip
        direction = direction / np.linalg.norm(direction)

        pelvis_rot = self.getPelvisRotation()
        if pelvis_rot is None:
            return None

        base_rotation = pelvis_rot["world"]
        local_direction = base_rotation.inv().apply(direction)

        rot_z = math.asin(local_direction[0]) * 180.0 / math.pi
        rot_x = math.atan2(-local_direction[2], -local_direction[1]) * 180.0 / math.pi

        if rot_x == -180:
            rot_x = 0

        euler = np.array([rot_x, 0, rot_z + 180])
        local = R.from_euler('zxy', [rot_z + 180, rot_x, 0], degrees=True)

        visibility = (self.getVisibility(self.RIGHT_HIP)+self.getVisibility(self.RIGHT_KNEE))/2

        self.right_hip_rotation = {
            "euler": euler,
            "local": local,
            "world": base_rotation * local,
            "visibility": visibility
        }

        return self.right_hip_rotation

    def getLeftKneeRotation(self):
        if self.left_knee_rotation is not None:
            return self.left_knee_rotation

        if not self.world_landmarks:
            return None

        LM = self.mp_pose.PoseLandmark
        left_knee = self.getLandmark(self.LEFT_KNEE)
        left_ankle = self.getLandmark(self.LEFT_ANKLE)

        direction = left_ankle - left_knee
        direction = direction / np.linalg.norm(direction)

        thigh_rot = self.getLeftHipRotation()
        if thigh_rot is None:
            return None

        base_rotation = thigh_rot["world"]
        local_direction = base_rotation.inv().apply(direction)

        rot_z = math.asin(-local_direction[0]) * 180.0 / math.pi
        rot_x = math.atan2(local_direction[2], local_direction[1]) * 180.0 / math.pi

        euler = np.array([rot_x, 0, rot_z])
        local = R.from_euler('zxy', [rot_z, rot_x, 0], degrees=True)

        visibility = (self.getVisibility(self.LEFT_KNEE)+self.getVisibility(self.LEFT_ANKLE))/2

        self.left_knee_rotation = {
            "euler": euler,
            "local": local,
            "world": base_rotation * local,
            "visibility": visibility
        }

        return self.left_knee_rotation

    def getRightKneeRotation(self):
        if self.right_knee_rotation is not None:
            return self.right_knee_rotation

        if not self.world_landmarks:
            return None

        LM = self.mp_pose.PoseLandmark
        right_knee = self.getLandmark(self.RIGHT_KNEE)
        right_ankle = self.getLandmark(self.RIGHT_ANKLE)

        direction = right_ankle - right_knee
        direction = direction / np.linalg.norm(direction)

        thigh_rot = self.getRightHipRotation()
        if thigh_rot is None:
            return None

        base_rotation = thigh_rot["world"]
        local_direction = base_rotation.inv().apply(direction)

        rot_z = math.asin(-local_direction[0]) * 180.0 / math.pi
        rot_x = math.atan2(local_direction[2], local_direction[1]) * 180.0 / math.pi

        euler = np.array([rot_x, 0, rot_z])
        local = R.from_euler('zxy', [rot_z, rot_x, 0], degrees=True)

        visibility = (self.getVisibility(self.RIGHT_KNEE)+self.getVisibility(self.RIGHT_ANKLE))/2

        self.right_knee_rotation = {
            "euler": euler,
            "local": local,
            "world": base_rotation * local,
            "visibility": visibility
        }

        return self.right_knee_rotation


    def getLandmark(self, landmark):
        if self.flipped:
            return np.array([self.world_landmarks.landmark[landmark].x, -self.world_landmarks.landmark[landmark].y, -self.world_landmarks.landmark[landmark].z])
        else:
            return np.array([-self.world_landmarks.landmark[landmark].x, -self.world_landmarks.landmark[landmark].y, -self.world_landmarks.landmark[landmark].z])
        
    def getVisibility(self, landmark):
        return self.world_landmarks.landmark[landmark].visibility
    
    def getPelvisAngle(self):
        return self.getPelvisRotation()["euler"][1]

    def getLeftElbowAngle(self):
        return np.degrees(self.getLeftElbowRotation()["local"].magnitude())
    
    def getRightElbowAngle(self):
        return np.degrees(self.getRightElbowRotation()["local"].magnitude())
    
    def getLeftKneeAngle(self):
        return np.degrees(self.getLeftKneeRotation()["local"].magnitude())
    
    def getRightKneeAngle(self):
        return np.degrees(self.getRightKneeRotation()["local"].magnitude())
    
    def computeRotations(self):
        self.getPelvisRotation()
        self.getTorsoRotation()
        self.getLeftElbowRotation()
        self.getRightElbowRotation()
        self.getLeftShoulderRotation()
        self.getRightShoulderRotation()
        self.getLeftHipRotation()
        self.getRightHipRotation()
        self.getLeftKneeRotation()
        self.getRightKneeRotation()

    def getAngleVector(self,format="euler"):
        self.computeRotations()

        if format.lower() == "euler":
            angles = np.zeros(19)
            angles[0] = self.pelvis_rotation["euler"][1]
            angles[1] = self.torso_rotation["euler"][0]
            angles[2] = self.torso_rotation["euler"][2]
            angles[3] = self.left_shoulder_rotation["euler"][0]
            angles[4] = self.left_shoulder_rotation["euler"][1]
            angles[5] = self.left_shoulder_rotation["euler"][2]
            angles[6] = self.right_shoulder_rotation["euler"][0]
            angles[7] = self.right_shoulder_rotation["euler"][1]
            angles[8] = self.right_shoulder_rotation["euler"][2]
            angles[9] = self.left_elbow_rotation["euler"][2]
            angles[10] = self.right_elbow_rotation["euler"][2]
            angles[11] = self.left_hip_rotation["euler"][0]
            angles[12] = self.left_hip_rotation["euler"][2]
            angles[13] = self.right_hip_rotation["euler"][0]
            angles[14] = self.right_hip_rotation["euler"][2]
            angles[15] = self.left_knee_rotation["euler"][0]
            angles[16] = self.left_knee_rotation["euler"][2]
            angles[17] = self.right_knee_rotation["euler"][0]
            angles[18] = self.right_knee_rotation["euler"][2]

            return angles
        else:
            angles = np.zeros(40)
            angles[0:4] = self.pelvis_rotation["local"].as_quat()
            angles[4:8] = self.torso_rotation["local"].as_quat()
            angles[8:12] = self.left_shoulder_rotation["local"].as_quat()
            angles[12:16] = self.right_shoulder_rotation["local"].as_quat()
            angles[16:20] = self.left_elbow_rotation["local"].as_quat()
            angles[20:24] = self.right_elbow_rotation["local"].as_quat()
            angles[24:28] = self.left_hip_rotation["local"].as_quat()
            angles[28:32] = self.right_hip_rotation["local"].as_quat()
            angles[32:36] = self.left_knee_rotation["local"].as_quat()
            angles[36:40] = self.right_knee_rotation["local"].as_quat()

            return angles