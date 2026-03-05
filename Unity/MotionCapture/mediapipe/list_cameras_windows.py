import cv2

def preview_cameras(max_index=10):
    caps = []
    active_ids = []

    for i in range(max_index):
        cap = cv2.VideoCapture(i, cv2.CAP_DSHOW)
        if cap.isOpened():
            print(f"[{i}] OPENED")
            caps.append(cap)
            active_ids.append(i)
        else:
            cap.release()

    if not caps:
        print("No cameras found.")
        return

    print("Press ESC to exit.")

    while True:
        for cam_id, cap in zip(active_ids, caps):
            ret, frame = cap.read()
            if ret:
                cv2.imshow(f"Camera {cam_id}", frame)

        if cv2.waitKey(1) & 0xFF == 27:  # ESC
            break

    for cap in caps:
        cap.release()
    cv2.destroyAllWindows()

if __name__ == "__main__":
    preview_cameras(10)