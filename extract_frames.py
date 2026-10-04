import cv2
import os

video_path = r"D:\unity_training\ROOM\Assets\Art\Images\start_menu_dreamcore_loop_v3.mp4"
out_dir = r"D:\unity_training\Practice\temp_frames"
os.makedirs(out_dir, exist_ok=True)

cap = cv2.VideoCapture(video_path)
fps = cap.get(cv2.CAP_PROP_FPS)
total = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
print(f"FPS: {fps}, Total frames: {total}, Duration: {total/fps:.2f}s")

# Extract every 15th frame (0.5s intervals)
frame_idx = 0
saved = 0
while True:
    ret, frame = cap.read()
    if not ret:
        break
    if frame_idx % 15 == 0:
        out_path = os.path.join(out_dir, f"f{frame_idx:04d}.png")
        cv2.imwrite(out_path, frame)
        saved += 1
        print(f"Saved frame {frame_idx} -> {out_path}")
    frame_idx += 1

cap.release()
print(f"Total saved: {saved} frames")
