@echo off
setlocal

rem Windows launcher for the MediaPipe / UPose / UDP pose system.
rem Run this file from anywhere; it will switch to this script folder.
cd /d "%~dp0"

rem IMPORTANT:
rem Camera indexes are machine-dependent.
rem Confirm camera indexes first with:
rem   python list_cameras_windows.py
rem Then edit CAM_P1..CAM_P4 below if needed.

set CONDA_ENV=mediapipe

set CAM_P1=0
set CAM_P2=1
set CAM_P3=2
set CAM_P4=3

rem Port mapping:
rem P1: camera %CAM_P1% -> Unity solo 52733, aggregator input 52833
rem P2: camera %CAM_P2% -> Unity solo 52734, aggregator input 52834
rem P3: camera %CAM_P3% -> Unity solo 52735, aggregator input 52835
rem P4: camera %CAM_P4% -> Unity solo 52736, aggregator input 52836
rem Aggregator output -> Unity collective 53000

echo Starting pose system from:
echo %CD%
echo.
echo Confirm camera indexes with: python list_cameras_windows.py
echo Conda environment: %CONDA_ENV%
echo.

start "Pose Aggregator 52833-52836 to 53000" cmd /k "call conda activate %CONDA_ENV% && python aggregator.py"

timeout /t 2 /nobreak >nul

start "MediaPipe P1 cam%CAM_P1% unity52733 agg52833" cmd /k "call conda activate %CONDA_ENV% && python run_mediapipe.py %CAM_P1% 52733 52833"
start "MediaPipe P2 cam%CAM_P2% unity52734 agg52834" cmd /k "call conda activate %CONDA_ENV% && python run_mediapipe.py %CAM_P2% 52734 52834"
start "MediaPipe P3 cam%CAM_P3% unity52735 agg52835" cmd /k "call conda activate %CONDA_ENV% && python run_mediapipe.py %CAM_P3% 52735 52835"
start "MediaPipe P4 cam%CAM_P4% unity52736 agg52836" cmd /k "call conda activate %CONDA_ENV% && python run_mediapipe.py %CAM_P4% 52736 52836"

echo.
echo Launched aggregator and four MediaPipe capture windows.
echo Keep these terminal windows open to view logs.
echo Press Esc in each OpenCV preview window to stop capture.
echo Use Ctrl+C in the aggregator terminal to stop aggregator.py.
echo.

endlocal
