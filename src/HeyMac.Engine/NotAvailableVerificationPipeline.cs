using HeyMac.Core.Models;
using HeyMac.Core.Recognition;
using HeyMac.Core.Security;

namespace HeyMac.Engine;

/// <summary>
/// Fallback verification pipeline used when the ONNX model files are missing.
/// Reports "not enrolled" / "models missing" so the UI can tell the user what to do.
/// See PORT_GUIDE.md for how to convert HeyMac's Core ML models to ONNX.
/// </summary>
public sealed class NotAvailableVerificationPipeline : IVerificationPipeline
{
    private readonly SecureStore _store;
    private readonly string _reason;

    public NotAvailableVerificationPipeline(SecureStore store, string reason)
    {
        _store = store;
        _reason = reason;
        Config = new PipelineConfig();
    }

    public (FaceEmbedding Embedding, LivenessResult Liveness) EnrollmentSample(System.Drawing.Bitmap image)
        => throw new VerificationPipelineException(_reason);

    public FaceEmbedding SaveEnrollment(IReadOnlyList<FaceEmbedding> embeddings)
    {
        if (embeddings.Count == 0)
            throw new VerificationPipelineException("no images provided");
        // Cannot embed without the ArcFace model.
        throw new VerificationPipelineException(_reason);
    }

    public FaceEmbedding LoadEnrolledCentroid()
    {
        // Even if a centroid exists on disk we can't compare without ArcFace.
        throw new VerificationPipelineException(_reason);
    }

    public FrameEvaluation Evaluate(System.Drawing.Bitmap image, FaceEmbedding centroid)
        => throw new VerificationPipelineException(_reason);

    public bool Verify(System.Drawing.Bitmap image) => false;

    public PipelineConfig Config { get; }
}
