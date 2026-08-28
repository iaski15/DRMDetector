@echo off
rem Publishes DRM Detector as a single self-contained exe into .\publish
dotnet publish "DRMDetector.csproj" -c Release --nologo
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
if exist "publish\DRMDetector.exe" (
    echo Done. DRMDetector.exe is in .\publish
) else (
    echo DRMDetector.exe not found after publish.
    exit /b 1
)
