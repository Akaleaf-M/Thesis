import cv2

cap = cv2.VideoCapture(0)  # 内置摄像头通常是 0
print("isOpened:", cap.isOpened())
ret, frame = cap.read()
print("read:", ret, "frame:", None if frame is None else frame.shape)
cap.release()
