namespace HeyMac.Engine;

using HeyMac.Core.Recognition;
using HeyMac.Core.Security;
using System.Drawing;

/// <summary>
/// Wires the Windows port together, mirroring HeyMacRuntime:
/// SecureStore (DPAPI key) + VerificationPipeline (OpenCV detector + ONNX models)
/// + camera + lock-screen environment. Produces verifiers/enrollers for the engine.
///
/// If the ONNX model files are missing, falls back to a stub pipeline that reports
/// "models missing" so the app still starts and the UI can guide the user.
/// See PORT_GUIDE.md.
/// </summary>
public sealed class HeyMacRuntime : IDisposable
{
    private readonly SecureStore _store;
    private readonly IVerificationPipeline _pipeline;
    private readonly IFrameSource _camera;
    public bool ModelsAvailable { get; }

    public HeyMacRuntime(string storeDirectory, string arcFaceModelPath, string antiSpoofModelPath,
        int cameraDeviceIndex = 0)
    {
        ISymmetricKeyProvider keyProvider = new DpapiKeyProvider();
        _store = new SecureStore(keyProvider, storeDirectory);

        IFaceDetector detector = new OpenCvFaceDetector();

        try
        {
            IFaceEmbedder embedder = new OnnxFaceEmbedder(arcFaceModelPath);
            IAntiSpoofClassifier antiSpoof = new OnnxAntiSpoofClassifier(antiSpoofModelPath);
            _pipeline = new VerificationPipeline(detector, embedder, antiSpoof, _store);
            ModelsAvailable = true;
        }
        catch (Exception ex)
        {
            // Models missing or invalid — fall back gracefully so the app still launches.
            _pipeline = new NotAvailableVerificationPipeline(_store,
                $"Face models not available: {ex.Message}. See PORT_GUIDE.md.");
            ModelsAvailable = false;
        }

        _camera = new CameraFrameSource(cameraDeviceIndex);
    }

    public SecureStore BackgroundStore => _store;
    public IVerificationPipeline Pipeline => _pipeline;
    public IFrameSource Camera => _camera;

    public IFaceMatcher Verifier(bool interactive = false, VerificationStrictness strictness = VerificationStrictness.Normal)
    {
        return new FaceVerifier(_camera, _pipeline);
    }

    public Enroller Enroller() => new Enroller(_camera, _pipeline);

    public void Dispose()
    {
        _camera.Dispose();
    }
}
