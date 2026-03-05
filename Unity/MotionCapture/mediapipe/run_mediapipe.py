import sys
import cv2
import mediapipe as mp
from clientUDP import ClientUDP
import time
from upose import UPose

camera_id = int(sys.argv[1]) if len(sys.argv) > 1 else 0
port = int(sys.argv[2]) if len(sys.argv) > 2 else 52733
pose_tracker = UPose(source="mediapipe",flipped=True)
mp_pose = mp.solutions.pose
mp_drawing = mp.solutions.drawing_utils
timeSincePostStatistics = 0

client = ClientUDP('127.0.0.1', port)
client.start()

if sys.platform.startswith("darwin"):
    cap = cv2.VideoCapture(camera_id, cv2.CAP_AVFOUNDATION)
elif sys.platform.startswith("win"):
    cap = cv2.VideoCapture(camera_id, cv2.CAP_DSHOW)
else:
    cap = cv2.VideoCapture(camera_id)  # Linux / fallback

if not cap.isOpened():
    raise RuntimeError(f"Failed to open camera index {camera_id}")

with mp_pose.Pose(min_detection_confidence=0.80, min_tracking_confidence=0.5, model_complexity = 2, static_image_mode = False,enable_segmentation = True) as pose: 
    while cap.isOpened():
        ti = time.time()
        success, image = cap.read()
        if not success:
            break

        image = cv2.flip(image, 1)
        
        image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
        results = pose.process(image)
        tf = time.time()

        image = cv2.cvtColor(image, cv2.COLOR_RGB2BGR)
        if results.pose_landmarks:

            if time.time()-timeSincePostStatistics>=1:
                print("Theoretical Maximum FPS: %f"%(1/(tf-ti)))
                timeSincePostStatistics = time.time()

            mp_drawing.draw_landmarks(image, results.pose_landmarks, mp_pose.POSE_CONNECTIONS, 
                mp_drawing.DrawingSpec(color=(255, 100, 0), thickness=2, circle_radius=4),
                mp_drawing.DrawingSpec(color=(255, 255, 255), thickness=2, circle_radius=2),
            )

        cv2.imshow(f'MediaPipe Pose cam{camera_id}', image)
        if cv2.waitKey(5) & 0xFF == 27:
            break

        data = "mprot\n"
        if results.pose_world_landmarks:
            #Calculate rotations
            pose_tracker.newFrame(results)
            pose_tracker.computeRotations();
            pelvis_rotation = pose_tracker.getPelvisRotation()["local"].as_quat()
            pelvis_visibility = pose_tracker.getPelvisRotation()["visibility"]
            torso_rotation = pose_tracker.getTorsoRotation()["local"].as_quat()
            torso_visibility = pose_tracker.getTorsoRotation()["visibility"]
            left_shoulder_rotation = pose_tracker.getLeftShoulderRotation()["local"].as_quat()
            left_shoulder_visibility = pose_tracker.getLeftShoulderRotation()["visibility"]
            right_shoulder_rotation = pose_tracker.getRightShoulderRotation()["local"].as_quat()
            right_shoulder_visibility = pose_tracker.getRightShoulderRotation()["visibility"]
            left_elbow_rotation = pose_tracker.getLeftElbowRotation()["local"].as_quat()
            left_elbow_visibility = pose_tracker.getLeftElbowRotation()["visibility"]
            right_elbow_rotation = pose_tracker.getRightElbowRotation()["local"].as_quat()
            right_elbow_visibility = pose_tracker.getRightElbowRotation()["visibility"]
            left_hip_rotation = pose_tracker.getLeftHipRotation()["local"].as_quat()
            left_hip_visibility = pose_tracker.getLeftHipRotation()["visibility"]
            right_hip_rotation = pose_tracker.getRightHipRotation()["local"].as_quat()
            right_hip_visibility = pose_tracker.getRightHipRotation()["visibility"]
            left_knee_rotation = pose_tracker.getLeftKneeRotation()["local"].as_quat()
            left_knee_visibility = pose_tracker.getLeftKneeRotation()["visibility"]
            right_knee_rotation = pose_tracker.getRightKneeRotation()["local"].as_quat()
            right_knee_visibility = pose_tracker.getRightKneeRotation()["visibility"]
            
            data += "{}|{}|{}|{}|{}|{}\n".format(0,pelvis_rotation[0],pelvis_rotation[1],pelvis_rotation[2],pelvis_rotation[3],pelvis_visibility)
            data += "{}|{}|{}|{}|{}|{}\n".format(1,torso_rotation[0],torso_rotation[1],torso_rotation[2],torso_rotation[3],torso_visibility)
            data += "{}|{}|{}|{}|{}|{}\n".format(2,left_shoulder_rotation[0],left_shoulder_rotation[1],left_shoulder_rotation[2],left_shoulder_rotation[3],left_shoulder_visibility)
            data += "{}|{}|{}|{}|{}|{}\n".format(3,right_shoulder_rotation[0],right_shoulder_rotation[1],right_shoulder_rotation[2],right_shoulder_rotation[3],right_shoulder_visibility)
            data += "{}|{}|{}|{}|{}|{}\n".format(4,left_elbow_rotation[0],left_elbow_rotation[1],left_elbow_rotation[2],left_elbow_rotation[3],left_elbow_visibility)
            data += "{}|{}|{}|{}|{}|{}\n".format(5,right_elbow_rotation[0],right_elbow_rotation[1],right_elbow_rotation[2],right_elbow_rotation[3],right_elbow_visibility)
            data += "{}|{}|{}|{}|{}|{}\n".format(6,left_hip_rotation[0],left_hip_rotation[1],left_hip_rotation[2],left_hip_rotation[3],left_hip_visibility)
            data += "{}|{}|{}|{}|{}|{}\n".format(7,right_hip_rotation[0],right_hip_rotation[1],right_hip_rotation[2],right_hip_rotation[3],right_hip_visibility)
            data += "{}|{}|{}|{}|{}|{}\n".format(8,left_knee_rotation[0],left_knee_rotation[1],left_knee_rotation[2],left_knee_rotation[3],left_knee_visibility)
            data += "{}|{}|{}|{}|{}|{}\n".format(9,right_knee_rotation[0],right_knee_rotation[1],right_knee_rotation[2],right_knee_rotation[3],right_knee_visibility)

            client.sendMessage(data)

cap.release()
cv2.destroyAllWindows()
