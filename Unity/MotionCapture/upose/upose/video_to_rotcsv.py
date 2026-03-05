import cv2
import mediapipe as mp
from upose import UPose   # 确保 upose.py 在同一文件夹或 PYTHONPATH 下
import csv
import argparse
import os
import time


def process_video_to_csv(video_path, output_csv, flipped=True, min_det=0.8, min_track=0.5):
    """
    从视频中提取 Mediapipe 姿态，并通过 UPose 计算局部关节旋转，
    将每一帧的 10 个关节四元数写入 CSV。
    """

    # 初始化 Mediapipe Pose
    mp_pose = mp.solutions.pose
    pose_tracker = UPose(source="mediapipe", flipped=flipped)

    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        print(f"Error: cannot open video file: {video_path}")
        return

    # 创建 CSV 文件并写入表头
    os.makedirs(os.path.dirname(output_csv), exist_ok=True) if os.path.dirname(output_csv) else None
    csv_file = open(output_csv, "w", newline="")
    writer = csv.writer(csv_file)

    # 表头：frame + 10 个关节 × 4 分量（x,y,z,w）
    writer.writerow([
        "frame",
        "pelvis_x","pelvis_y","pelvis_z","pelvis_w",
        "torso_x","torso_y","torso_z","torso_w",
        "l_shoulder_x","l_shoulder_y","l_shoulder_z","l_shoulder_w",
        "r_shoulder_x","r_shoulder_y","r_shoulder_z","r_shoulder_w",
        "l_elbow_x","l_elbow_y","l_elbow_z","l_elbow_w",
        "r_elbow_x","r_elbow_y","r_elbow_z","r_elbow_w",
        "l_hip_x","l_hip_y","l_hip_z","l_hip_w",
        "r_hip_x","r_hip_y","r_hip_z","r_hip_w",
        "l_knee_x","l_knee_y","l_knee_z","l_knee_w",
        "r_knee_x","r_knee_y","r_knee_z","r_knee_w",
    ])

    frame_index = 0
    processed_frames = 0
    start_time = time.time()

    with mp_pose.Pose(
        min_detection_confidence=min_det,
        min_tracking_confidence=min_track,
        model_complexity=2,
        static_image_mode=False,
        enable_segmentation=False
    ) as pose:

        while True:
            success, image = cap.read()
            if not success:
                break  # 视频结束

            # BGR → RGB
            image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)

            # 可选：水平翻转，用于模拟镜像摄像头（和你之前 webcam 脚本一致）
            if flipped:
                image_rgb = cv2.flip(image_rgb, 1)

            # Mediapipe 推理
            results = pose.process(image_rgb)

            if results.pose_world_landmarks:
                # 把这一帧喂给 UPose，计算 local 旋转
                pose_tracker.newFrame(results)
                pose_tracker.computeRotations()

                pelvis_rotation = pose_tracker.getPelvisRotation()["local"].as_quat()
                torso_rotation = pose_tracker.getTorsoRotation()["local"].as_quat()
                left_shoulder_rotation = pose_tracker.getLeftShoulderRotation()["local"].as_quat()
                right_shoulder_rotation = pose_tracker.getRightShoulderRotation()["local"].as_quat()
                left_elbow_rotation = pose_tracker.getLeftElbowRotation()["local"].as_quat()
                right_elbow_rotation = pose_tracker.getRightElbowRotation()["local"].as_quat()
                left_hip_rotation = pose_tracker.getLeftHipRotation()["local"].as_quat()
                right_hip_rotation = pose_tracker.getRightHipRotation()["local"].as_quat()
                left_knee_rotation = pose_tracker.getLeftKneeRotation()["local"].as_quat()
                right_knee_rotation = pose_tracker.getRightKneeRotation()["local"].as_quat()

                # 写入 CSV：一帧
                writer.writerow([
                    frame_index,
                    *pelvis_rotation,
                    *torso_rotation,
                    *left_shoulder_rotation,
                    *right_shoulder_rotation,
                    *left_elbow_rotation,
                    *right_elbow_rotation,
                    *left_hip_rotation,
                    *right_hip_rotation,
                    *left_knee_rotation,
                    *right_knee_rotation,
                ])

                processed_frames += 1

            frame_index += 1

    cap.release()
    csv_file.close()

    elapsed = time.time() - start_time
    print(f"Done. Total frames: {frame_index}, frames with pose: {processed_frames}")
    print(f"Saved rotation CSV to: {output_csv}")
    if elapsed > 0:
        print(f"Avg FPS (including non-pose frames): {frame_index / elapsed:.2f}")


def main():
    parser = argparse.ArgumentParser(description="Extract pose rotations from video to CSV (for UPose in Unity).")
    parser.add_argument("--video", "-v", type=str, required=True,
                        help="Path to input video file.")
    parser.add_argument("--output", "-o", type=str, default="pose_rotations.csv",
                        help="Path to output CSV file.")
    parser.add_argument("--no_flip", action="store_true",
                        help="Disable horizontal flipping (default: flipped=True).")
    parser.add_argument("--min_det", type=float, default=0.8,
                        help="Mediapipe min_detection_confidence.")
    parser.add_argument("--min_track", type=float, default=0.5,
                        help="Mediapipe min_tracking_confidence.")

    args = parser.parse_args()

    flipped = not args.no_flip
    process_video_to_csv(
        video_path=args.video,
        output_csv=args.output,
        flipped=flipped,
        min_det=args.min_det,
        min_track=args.min_track
    )


if __name__ == "__main__":
    main()
