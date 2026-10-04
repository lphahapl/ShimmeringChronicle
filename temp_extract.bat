@echo off
chcp 65001 >nul 2>&1

copy "C:\Users\鲁迅\AppData\Local\Programs\Trae CN\resources\app\bin\ffmpeg.exe" "D:\unity_training\Practice\ffmpeg.exe" >nul 2>&1
copy "C:\Users\鲁迅\AppData\Local\Programs\Trae CN\resources\app\bin\ffprobe.exe" "D:\unity_training\Practice\ffprobe.exe" >nul 2>&1

mkdir "D:\unity_training\Practice\temp_frames" 2>nul

"D:\unity_training\Practice\ffmpeg.exe" -y -i "D:\unity_training\ROOM\Assets\Art\Images\start_menu_dreamcore_loop_v3.mp4" -vf "fps=2" "D:\unity_training\Practice\temp_frames\f%%04d.png"

dir "D:\unity_training\Practice\temp_frames\" /b
