### Hey Mac Windows Port

This is an unofficial Windows port of [iharshitmaurya/HeyMac](https://github.com/iharshitmaurya/HeyMac) — face-based lock screen unlock and app shielding for Windows, reimplemented in C#/.NET 8.

## What it does

* **Face login:** Enroll your face via webcam; unlock your Windows lock screen by being recognized (100% on-device).
* **App Lock:** Shield sensitive apps (e.g. WhatsApp, Signal, Mail) with a blur overlay until your face is verified.
* **Private by design:** Face embeddings are encrypted at rest (AES-GCM) with a DPAPI-protected key. No face images are stored. No data leaves your machine.

## Project structure

```
HeyMac.Windows/
├── src/
│   ├── HeyMac.Core/          # Recognition + security (ONNX Runtime, AES-GCM, DPAPI)
│   ├── HeyMac.Engine/        # Camera (OpenCV), Win32, lock-screen watcher, face verifier
│   └── HeyMac.App/           # WPF UI + tray icon + app-lock window shielding
├── PORT_GUIDE.md             # Reverse-engineering notes + macOS→Windows mapping
├── convert_models.py         # Convert HeyMac's Core ML models to ONNX
├── Dockerfile.convert        # Docker image for model conversion
└── Build.ps1                 # PowerShell build script
```

## Prerequisites

* Windows 10 / 11
* .NET 8 SDK
* A webcam
* Docker (only needed to convert the Core ML models to ONNX)

## Build

```powershell
.\Build.ps1 -Configuration Release
```

## Models (required before running)

HeyMac ships Apple-only Core ML models. Convert them to ONNX first:

```powershell
docker build -t heymac-model-convert -f Dockerfile.convert .
docker run --rm -v C:\path\to\HeyMac:/src/heymac -v C:\out\models:/out heymac-model-convert
```

Copy the resulting `arcface.onnx` and `antispoof.onnx` into:

```
src\HeyMac.App\bin\Release\net8.0-windows\models\
```

## Important limitations

* **Secure desktop:** Windows runs the lock screen on the isolated Winlogon desktop. A normal user app cannot type into it. To auto-type your password at the lock screen, deploy the engine as a service or scheduled task with logon-desktop access.
* **Face detection:** Without OpenCV's YuNet model, the app falls back to Haar cascades with estimated landmarks (lower accuracy).
* **No notch island:** The macOS "Dynamic Notch Island" UI has no Windows equivalent; this port uses a tray icon + settings window.

## Disclaimer

This is an unofficial port. It is not affiliated with, endorsed by, or connected to the original HeyMac macOS app or its author.

## License

MIT
