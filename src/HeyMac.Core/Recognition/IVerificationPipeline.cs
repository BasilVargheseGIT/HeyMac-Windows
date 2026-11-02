using System.Drawing;
using HeyMac.Core.Models;
using HeyMac.Core.Security;

namespace HeyMac.Core.Recognition;

/// <summary>
/// Abstraction over face detection + 5-landmark localization.
/// HeyMac used Apple Vision; on Windows this is implemented with OpenCVSharp DNN (or MediaPipe).
/// </summary>
public interface IFaceDetector
{
    /// <summary>Detects the largest face in the image and returns its box + 5 landmarks.</summary>
    DetectedFace DetectLargestFace(Bitmap image);
}

/// <summary>
/// Abstraction over the ArcFace embedding model.
/// HeyMac used Core ML (ArcFace.mlpkgdata, w600k_mbf); on Windows this runs via ONNX Runtime.
/// </summary>
public interface IFaceEmbedder
{
    FaceEmbedding Embed(DetectedFace face, Bitmap frame);
}

/// <summary>
/// Abstraction over the anti-spoof / liveness model.
/// HeyMac used Core ML (AntiSpoof.mlpkgdata, MiniFASNetV2); on Windows this runs via ONNX Runtime.
/// </summary>
public interface IAntiSpoofClassifier
{
    LivenessResult Classify(DetectedFace face, Bitmap frame);
    /// <summary>Classify an already-prepared 80x80 patch.</summary>
    LivenessResult ClassifyPatch(Bitmap patch);
}

/// <summary>
/// End-to-end verification: detect -> align -> embed -> anti-spoof -> compare to enrolled centroid.
/// Port of HeyMac's VerificationPipeline.
/// </summary>
public interface IVerificationPipeline
{
    /// <summary>One enrollment sample: must contain a live face (so a photo can't be enrolled).</summary>
    (FaceEmbedding Embedding, LivenessResult Liveness) EnrollmentSample(Bitmap image);

    /// <summary>Saves the normalized centroid of embeddings as the enrolled identity.</summary>
    FaceEmbedding SaveEnrollment(IReadOnlyList<FaceEmbedding> embeddings);

    /// <summary>Loads the previously enrolled centroid from secure storage.</summary>
    FaceEmbedding LoadEnrolledCentroid();

    /// <summary>Evaluates a single frame against a given centroid.</summary>
    FrameEvaluation Evaluate(Bitmap image, FaceEmbedding centroid);

    /// <summary>Verifies a single frame against the stored enrollment.</summary>
    bool Verify(Bitmap image);

    PipelineConfig Config { get; }
}
