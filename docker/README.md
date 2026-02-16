# Docker Build (Linux / Mono)

This folder provides a Docker-based build environment for BK7231GUIFlashTool.

It allows building the project on Linux without requiring Windows or Visual Studio.

## Requirements

- Docker
- Python 3

## Build

From inside the `docker` folder:

```powershell
python build_app.py
```

After completion EXE file will be in docker/release/
