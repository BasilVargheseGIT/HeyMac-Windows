using System.Drawing;
using HeyMac.Core.Models;
using HeyMac.Core.Math;
using HeyMac.Core.Security;

namespace HeyMac.Core.Recognition;

/// <summary>
/// End-to-end verification pipeline. Port of HeyMac's VerificationPipeline.
///
/// Flow per frame:
///   1. Detect largest face + 5 landmarks
///   2. Align to ArcFace 112x112 template (similarity transform + bilinear warp)
///   3. Run ArcFace embedder -> L2-normalized embedding
///   4. Run anti-spoof classifier (MiniFASNet) on a 2.7x-cropped 80x80 patch
///   5. Cosine similarity vs enrolled centroid; accept if live && similarity >= threshold
///
/// Enrollment requires each sample to pass liveness (so a photo can't be enrolled).
/// The enrolled identity = normalized centroid of sample embeddings, encrypted at rest.
/// </summary>
public sealed class VerificationPipeline : IVerificationPipeline
{
    private readonly IFaceDetector _detector;
    private readonly IFaceEmbedder _embedder;
    private readonly IAntiSpoofClassifier _classifier;
    private readonly SecureStore _store;
    public PipelineConfig Config { get; }

    public VerificationPipeline(IFaceDetector detector, IFaceEmbedder embedder,
        IAntiSpoofClassifier classifier, SecureStore store, PipelineConfig? config = null)
    {
        _detector = detector;
        _embedder = embedder;
        _classifier = classifier;
        _store = store;
        Config = config ?? new PipelineConfig();
    }

    public (FaceEmbedding Embedding, LivenessResult Liveness) EnrollmentSample(Bitmap image)
    {
        DetectedFace face = Detect(image);
        LivenessResult liveness = _classifier.Classify(face, image);
        if (!IsLive(liveness))
            throw new VerificationPipelineException("frame failed liveness check", liveness.Confidence);
        return (_embedder.Embed(face, image), liveness);
    }

    public FaceEmbedding SaveEnrollment(IReadOnlyList<FaceEmbedding> embeddings)
    {
        if (embeddings.Count == 0)
            throw new VerificationPipelineException("no images provided");

        var normalized = embeddings.Select(EmbeddingMath.Normalize).ToList();
        FaceEmbedding centroid = EmbeddingMath.Normalize(EmbeddingMath.Centroid(normalized));

        byte[] data = EmbeddingToBytes(centroid);
        _store.Save(data, SecureStore.CentroidKey);
        return centroid;
    }

    public FaceEmbedding LoadEnrolledCentroid()
    {
        byte[] stored = _store.Load(SecureStore.CentroidKey);
        if (stored.Length == 0 || stored.Length % 4 != 0)
            throw new VerificationPipelineException("corrupt enrollment");

        float[] floats = new float[stored.Length / 4];
        Buffer.BlockCopy(stored, 0, floats, 0, stored.Length);
        return new FaceEmbedding(floats);
    }

    public FrameEvaluation Evaluate(Bitmap image, FaceEmbedding centroid)
    {
        DetectedFace face = Detect(image);
        LivenessResult liveness = _classifier.Classify(face, image);
        FaceEmbedding candidate = _embedder.Embed(face, image);

        float similarity = candidate.Vector.Count == centroid.Vector.Count
            ? EmbeddingMath.CosineSimilarity(centroid, candidate)
            : -1f;

        bool isLive = IsLive(liveness);
        bool isMatch = similarity >= Config.MatchThreshold;
        return new FrameEvaluation(liveness.Confidence, similarity, isLive, isMatch);
    }

    public bool Verify(Bitmap image) =>
        Evaluate(image, LoadEnrolledCentroid()).Accepted;

    private DetectedFace Detect(Bitmap image)
    {
        DetectedFace face = _detector.DetectLargestFace(image);
        if (face.Landmarks.Count != 5)
            throw new VerificationPipelineException("no face detected");
        return face;
    }

    private static bool IsLive(LivenessResult result) =>
        result.IsLive && result.Confidence >= 0.6f; // livenessThreshold default

    private static byte[] EmbeddingToBytes(FaceEmbedding e)
    {
        float[] floats = e.Vector.ToArray();
        byte[] bytes = new byte[floats.Length * 4];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}

public sealed class VerificationPipelineException : Exception
{
    public float? LivenessConfidence { get; }
    public VerificationPipelineException(string message, float? confidence = null)
        : base(message) => LivenessConfidence = confidence;
}
