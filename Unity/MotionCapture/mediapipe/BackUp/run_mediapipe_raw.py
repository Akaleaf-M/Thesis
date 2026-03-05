import cv2
import mediapipe as mp
from clientUDP import ClientUDP
import time
import numpy as np

mp_pose = mp.solutions.pose
mp_drawing = mp.solutions.drawing_utils
timeSincePostStatistics = 0

client = ClientUDP('127.0.0.1',52733)
client.start()

cap = cv2.VideoCapture(0)  # Open webcam

FLIP_CAMERA = False

LM = mp.solutions.pose.PoseLandmark


map = np.arange(33)

if FLIP_CAMERA:
    map[LM.LEFT_SHOULDER.value] = LM.RIGHT_SHOULDER.value
    map[LM.RIGHT_SHOULDER.value] = LM.LEFT_SHOULDER.value
    map[LM.LEFT_ELBOW.value] = LM.RIGHT_ELBOW.value
    map[LM.RIGHT_ELBOW.value] = LM.LEFT_ELBOW.value
    map[LM.LEFT_WRIST.value] = LM.RIGHT_WRIST.value
    map[LM.RIGHT_WRIST.value] = LM.LEFT_WRIST.value
    map[LM.LEFT_HIP.value] = LM.RIGHT_HIP.value
    map[LM.RIGHT_HIP.value] = LM.LEFT_HIP.value
    map[LM.LEFT_KNEE.value] = LM.RIGHT_KNEE.value
    map[LM.RIGHT_KNEE.value] = LM.LEFT_KNEE.value
    map[LM.LEFT_ANKLE.value] = LM.RIGHT_ANKLE.value
    map[LM.RIGHT_ANKLE.value] = LM.LEFT_ANKLE.value

with mp_pose.Pose(min_detection_confidence=0.80, min_tracking_confidence=0.5, model_complexity = 2,static_image_mode = False,enable_segmentation = True) as pose: 
    while cap.isOpened():
        ti = time.time()
        success, image = cap.read()
        if not success:
            break

        if FLIP_CAMERA:
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

        cv2.imshow('MediaPipe Face Detection', image)
        if cv2.waitKey(5) & 0xFF == 27:
            break

        data = "mpxyz\n"
        i = 0
        if results.pose_world_landmarks:
            world_landmarks = results.pose_world_landmarks
            for i in range(0,33):
                if FLIP_CAMERA:
                    data += "{}|{}|{}|{}|{}\n".format(map[i],world_landmarks.landmark[i].x,world_landmarks.landmark[i].y,world_landmarks.landmark[i].z,world_landmarks.landmark[i].visibility)
                else:
                    data += "{}|{}|{}|{}|{}\n".format(map[i],-world_landmarks.landmark[i].x,world_landmarks.landmark[i].y,world_landmarks.landmark[i].z,world_landmarks.landmark[i].visibility)
                
            client.sendMessage(data)

cap.release()
cv2.destroyAllWindows()
