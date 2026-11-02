# Hey Mac — Windows Port (Reverse-Engineered)

This is a **C#/.NET 8 port** of [iharshitmaurya/HeyMac](https://github.com/iharshitmaurya/HeyMac), a macOS app that unlocks your Mac's lock screen and shields apps with Face ID.

Because HeyMac is built on macOS-only frameworks (Core ML, Apple Vision, SwiftUI/AppKit, Keychain, lock-screen integration, launch agents), this is a **reimplementation**, not a direct build. Every layer was reverse-engineered from the Swift source and reimplemented with Windows equivalents.

---

## What was reverse-engineered

| HeyMac layer (Swift) | Algorithm / mechanism | Windows replacement |
|---|---|---|
| `FaceEmbedder` (Core ML `ArcFace.mlpkgdata`, w600k_mbf) | 112×112 aligned-face embedding, L2-normalized | ONNX Runtime + `OnnxFaceEmbedder` |
| `FaceGeometry` (Vision landmarks) | Umeyama similarity transform → align to ArcFace 112×112 template; bilinear warp | `FaceGeometry` (pure C# port) |
| `AntiSpoofClassifier` (Core ML `AntiSpoof.mlpkgdata`, MiniFASNetV2 2.7_80x80) | 2.7× box crop → 80×80 BGR → 3-class liveness (class 1 = live) | ONNX Runtime + `OnnxAntiSpoofClassifier` |
| `VerificationPipeline` | detect → align → embed → anti-spoof → cosine similarity vs enrolled centroid (threshold 0.5; liveness ≥0.6; needs 2 consecutive accepted frames) | `VerificationPipeline` (exact port) |
| `SecureStore` + `KeychainKeyProvider` | AES-GCM encrypt face centroid + login password; key held in macOS Keychain | `SecureStore` (AES-GCM files) + `DpapiKeyProvider` (Windows DPAPI) |
| `LockScreenUnlocker` | Poll lock state; when locked run face verification; type stored password; stop after a stale password fails | `LockScreenUnlocker` (exact port of tick loop) |
| `FaceVerifier` | Bounded camera window; match = N consecutive live+similar frames | `FaceVerifier` |
| `Enroller` | Collect N live camera samples → save centroid | `Enroller` |
| `KeystrokeInjector` (CGEvent) | Type password into lock screen | `SendInput` (Win32) |
| `LockScreenEnvironment` (CGDisplay, AX) | Detect lock, wake display | `LockScreenDetector` (`OpenInputDesktop` secure-desktop test) |
| SwiftUI menu bar / notch island | Dynamic island UI during scan | WPF + Windows Forms NotifyIcon (tray) |

### Algorithms ported verbatim

The following were translated line-for-line from `Sources/HeyMacCore/Recognition/FaceGeometry.swift`:

- **`ArcFaceTemplate`** — InsightFace canonical 112×112 5-point landmark template.
- **`SimilarityTransform`** — Umeyama least-squares similarity transform (implemented as the complex-number reduction `z = Σ conj(p)·q / Σ\|p\|²` on mean-centered points).
- **`AlignFace`** — inverse-warp with bilinear sampling, edge-clamped (matches `cv2.warpAffine INTER_LINEAR` / `BORDER_REPLICATE`).
- **`AntiSpoofCropRect`** — Silent-Face-Anti-Spoofing `_get_new_box`: grow box 2.7× around center, clamp/shift into frame.
- **`Resize`** — cv2-style bilinear resize with half-pixel-center mapping, no antialiasing.

Thresholds kept identical to HeyMac: `matchThreshold = 0.5`, `livenessThreshold = 0.6`, 2 consecutive frames required, 6-frame mismatch streak, 6 s scan window.

---

## Project layout

```
HeyMac.Windows/
├── HeyMac.Windows.sln
├── src/
│   ├── HeyMac.Core/              # Pure .NET recognition + security (ONNX Runtime)
│   │   ├── Models/FaceEmbedding.cs
│   │   ├── Math/EmbeddingMath.cs
│   │   ├── Geometry/FaceGeometry.cs      # ported from Swift FaceGeometry
│   │   ├── Recognition/
│   │   │   ├── VerificationPipeline.cs   # ported from Swift VerificationPipeline
│   │   │   ├── OnnxFaceEmbedder.cs       # ArcFace via ONNX
│   │   │   ├── OnnxAntiSpoofClassifier.cs# MiniFASNet via ONNX
│   │   │   ├── IFaceDetector / IFaceEmbedder / IAntiSpoofClassifier / IVerificationPipeline
│   │   │   └── Onnx*.cs
│   │   ├── Tensors/TensorHelper.cs       # Bitmap → ONNX DenseTensor
│   │   └── Security/
│   │       ├── SecureStore.cs            # AES-GCM file store (ported)
│   │       ├── AesGcm.cs                 # AES-GCM seal/open (.NET AesGcm)
│   │       └── DpapiKeyProvider.cs       # DPAPI key protection (Keychain replacement)
│   ├── HeyMac.Engine/            # Windows engine + camera + Win32
│   │   ├── EngineSettings.cs
│   │   ├── VerificationOutcome.cs
│   │   ├── FaceVerifier.cs               # ported from Swift FaceVerifier
│   │   ├── LockScreenUnlocker.cs         # ported from Swift LockScreenUnlocker
│   │   ├── EngineController.cs           # ported from Swift EngineController
│   │   ├── Enroller.cs                   # ported from Swift Enroller
│   │   ├── HeyMacRuntime.cs              # wires store + pipeline + camera
│   │   ├── IFrameSource.cs / CameraFrame.cs
│   │   ├── ILockScreenEnvironment.cs / IPasswordTyping.cs
│   │   ├── EngineEvent.cs
│   │   ├── OpenCvFaceDetector.cs         # OpenCV YuNet/Haar face + landmarks
│   │   ├── Camera/CameraFrameSource.cs   # OpenCV VideoCapture
│   │   └── Win32/
│   │       ├── LockScreenDetector.cs     # OpenInputDesktop secure-desktop test
│   │       └── KeystrokeInjector.cs      # SendInput password typing
│   └── HeyMac.App/               # WPF UI + tray icon (menu-bar equivalent)
│       ├── App.xaml / App.xaml.cs
│       ├── MainWindow.xaml / MainWindow.xaml.cs
```

---

## Prerequisites

- **Windows 10/11**
- **.NET 8 SDK** (`dotnet --version` → 8.0.x)
- A webcam
- Visual Studio 2022 (or VS Code + C# Dev Kit) — or just `dotnet build`

## Build

```powershell
cd C:\Users\basil_l21rzzm\Documents\Default Project\HeyMac.Windows
dotnet build HeyMac.Windows.sln -c Release
dotnet run --project src\HeyMac.App\HeyMac.App.csproj -c Release
```

NuGet packages used:
- `Microsoft.ML.OnnxRuntime` (Core)
- `OpenCVSharp4` + `OpenCVSharp4.runtime.win` (Engine)

---

## Converting the Core ML models to ONNX

HeyMac ships two Core ML model packages under `Sources/HeyMacCore/Resources/`:
- `ArcFace.mlpkgdata` (w600k_mbf embedding)
- `AntiSpoof.mlpkgdata` (MiniFASNetV2 liveness)

`.mlmodel` is Apple-only. To run on Windows you must convert them to **ONNX**:

1. **Extract** the `model.mlmodel` from each `.mlpkgdata/Data/com.apple.CoreML/` folder.
2. **ArcFace** → use `coremltools` (Python) to convert to ONNX:
   ```bash
   pip install coremltools onnx
   python -c "
   import coremltools as ct
   model = ct.models.MLModel('ArcFace.mlmodel')
   onnx_model = model.convert_to('mlprogram')  # or use coremltools.converters.mil
   onnx_model.save('arcface.onnx')
   "
   ```
   (Exact API depends on coremltools version; see coremltools docs for `convert_to_onnx`.)
3. **AntiSpoof** — the repo already includes `scripts/convert_antispoof.py` (PyTorch → Core ML). Reverse that: convert the Core ML model back to ONNX with coremltools, or re-export the original PyTorch MiniFASNet to ONNX directly.
4. Place the resulting ONNX files in `src\HeyMac\App\bin\Release\net8.0-windows\models\` (or set paths in `HeyMacRuntime`).

**Expected model IO** (assumed by this port; adjust if your export differs):
- ArcFace: input `input_image` `[1,3,112,112]` NCHW float RGB `(x-127.5)/127.5` → output `embedding` `[1,512]`
- AntiSpoof: input `input_image` `[1,3,80,80]` NCHW float BGR raw 0-255 → output `probabilities` `[1,3]`

> Without these ONNX files, enrollment and verification throw "model not found." The UI will tell you where to put them.

---

## Important limitations (Windows vs macOS)

### 1. Typing into the Windows lock screen (secure desktop)
HeyMac types the password directly into macOS's lock screen via Core Graphics events. On Windows, the lock screen runs on the **secure desktop (Winlogon)**, which a normal user-mode app cannot send input to.

`KeystrokeInjector` uses `SendInput`, which works on the **interactive desktop**. To reach the actual lock screen you must deploy the app so it runs in the Winlogon context, e.g.:
- A Windows **service** configured to "Allow service to interact with desktop," or
- A **scheduled task** set to run whether user is logged on or not, with the launcher started via a **credential provider**, or
- Run as SYSTEM with `CreateProcessAsUser` into session 0 / Winlogon desktop.

Until then, the app verifies the face but can't auto-type into the locked workstation — this is a Windows security boundary, not a bug in the port.

### 2. Face detection landmarks
`OpenCvFaceDetector` uses OpenCV's **FaceDetectorYN (YuNet)** for real 5-point landmarks when you supply `face_detection_yunet_2023mar.onnx` (OpenCV Zoo). Without it, it falls back to Haar cascades with **box-estimated landmarks**, which makes ArcFace alignment approximate and lowers accuracy. Download the YuNet ONNX and pass its path to `OpenCvFaceDetector`.

### 3. No notch island / dynamic UI
macOS's "Dynamic Notch Island" has no Windows equivalent. The WPF app uses a tray icon + status window instead.

---

## File-by-file mapping (HeyMac → this port)

| HeyMac source | Windows port |
|---|---|
| `Sources/HeyMacCore/Recognition/VerificationPipeline.swift` | `src/HeyMac.Core/Recognition/VerificationPipeline.cs` |
| `Sources/HeyMacCore/Recognition/FaceEmbedder.swift` | `src/HeyMac.Core/Recognition/OnnxFaceEmbedder.cs` |
| `Sources/HeyMacCore/Recognition/AntiSpoofClassifier.swift` | `src/HeyMac.Core/Recognition/OnnxAntiSpoofClassifier.cs` |
| `Sources/HeyMacCore/Recognition/FaceGeometry.swift` | `src/HeyMac.Core/Geometry/FaceGeometry.cs` |
| `Sources/HeyMacCore/Recognition/Embedding.swift` | `src/HeyMac.Core/Models/FaceEmbedding.cs` + `Math/EmbeddingMath.cs` |
| `Sources/HeyMacCore/Security/SecureStore.swift` | `src/HeyMac.Core/Security/SecureStore.cs` |
| `Sources/HeyMacCore/Security/KeychainKeyProvider.swift` | `src/HeyMac.Core/Security/DpapiKeyProvider.cs` |
| `Sources/HeyMacEngine/LockScreenUnlocker.swift` | `src/HeyMac.Engine/LockScreenUnlocker.cs` |
| `Sources/HeyMacEngine/FaceVerifier.swift` | `src/HeyMac.Engine/FaceVerifier.cs` |
| `Sources/HeyMacEngine/Enroller.swift` | `src/HeyMac.Engine/Enroller.cs` |
| `Sources/HeyMacEngine/EngineController.swift` | `src/HeyMac.Engine/EngineController.cs` |
| `Sources/HeyMacEngine/KeystrokeInjector.swift` | `src/HeyMac.Engine/Win32/KeystrokeInjector.cs` |
| `Sources/HeyMacApp/` (SwiftUI UI) | `src/HeyMac.App/` (WPF) |

---

## Security notes

- Face data = L2-normalized ArcFace centroid, AES-GCM encrypted at rest.
- The AES wrapping key is protected with **Windows DPAPI** (CurrentUser scope) — the analog of HeyMac's Keychain item `com.heymac.sessionkey`.
- No face images are stored, only the numeric embedding centroid.
- Liveness (MiniFASNet) rejects photos/replay during both enrollment and verification.

---

## Next steps to make it fully functional

1. Convert + place the two ONNX models (ArcFace, AntiSpoof).
2. Obtain `face_detection_yunet_2023mar.onnx` for accurate landmarks (or train/use a better detector).
3. Deploy the engine as a service/scheduled task with logon-desktop access so `SendInput` reaches the lock screen.
4. Add the app-lock shielding layer (blur protected windows) using Win32 window enumeration + `SetWindowDisplayAffinity` / overlay windows — the Swift side lives in `Sources/HeyMacApp/AppLock/`.

This port is intentionally a starter: the recognition, security, and lock-screen logic are fully ported; model files and secure-desktop deployment remain environment-specific.
