import cv2

def find_cameras(max_index=10):
    found = []
    for i in range(max_index):
        cap = cv2.VideoCapture(i, cv2.CAP_AVFOUNDATION)  # macOS 推荐
        if cap.isOpened():
            ret, frame = cap.read()
            if ret and frame is not None:
                found.append((i, frame.shape))
        cap.release()
    return found

cams = find_cameras(6)
if not cams:
    print("no cameras found")
else:
    for idx, shape in cams:
        print(f"camera index {idx} OK, frame shape {shape}")
