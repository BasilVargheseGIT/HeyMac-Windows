using System.Drawing;
using HeyMac.Core.Geometry;
using HeyMac.Core.Tensors;

namespace HeyMac.Core.Recognition;

/// <summary>
/// ArcFace (w600k_mbf) embedding via ONNX Runtime. Port of HeyMac's FaceEmbedder.
///
/// Preprocessing reproduced from Core ML:
///   1. Align the detected face to the 112x112 ArcFace template (similarity transform)
///   2. NCHW float tensor, RGB channel order, (x - 127.5) / 127.5 normalization
///   3. L2-normalize the output embedding
///
/// `modelPath` should point to an ArcFace model converted from .mlmodel to ONNX
/// (see PORT_GUIDE.md). Input name "input_image", output "embedding".
/// </summary>
public sealed class OnnxFaceEmbedder : IFaceEmbedder, IDisposable
{
    private readonly Microsoft.ML.OnnxRuntime.InferenceSession _session;
    private readonly string _inputName = "input_image";
    private readonly string _outputName = "embedding";

    public OnnxFaceEmbedder(string modelPath)
    {
        if (!System.IO.File.Exists(modelPath))
            throw new FileNotFoundException("ArcFace ONNX model not found", modelPath);

        var options = new SessionOptions();
        // Prefer the CPU execution provider; add CUDA/TensorRT if available on the machine.
        options.AppendExecutionProvider_CPU(0);
        _session = new InferenceSession(modelPath, options);
    }

    public FaceEmbedding Embed(DetectedFace face, Bitmap frame)
    {
        if (face.Landmarks.Count != 5)
            throw new VerificationPipelineException("no face detected");

        Bitmap aligned = FaceGeometry.AlignFace(frame, face.Landmarks, 112);
        try
        {
            // NCHW, RGB, normalized (x-127.5)/127.5. channelOrder {2,1,0} converts
            // the GDI+ bitmap's B,G,R storage into R,G,B tensor channels.
            var input = TensorHelper.FromBitmap(
                aligned, TensorHelper.Layout.NCHW, new[] { 2, 1, 0 }, normalize: true);

            using var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, input) };
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);

            var output = results.FirstOrDefault(r => r.Name == _outputName)
                ?.AsTensor<float>()
                ?? throw new VerificationPipelineException("ArcFace output tensor missing");

            float[] vector = new float[output.Length];
            int i = 0;
            foreach (float v in output) vector[i++] = v;

            return EmbeddingMath.Normalize(new FaceEmbedding(vector));
        }
        finally
        {
            aligned.Dispose();
        }
    }

    public void Dispose() => _session.Dispose();
}
