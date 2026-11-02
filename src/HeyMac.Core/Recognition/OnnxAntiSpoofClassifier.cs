using System.Drawing;
using HeyMac.Core.Geometry;
using HeyMac.Core.Tensors;

namespace HeyMac.Core.Recognition;

/// <summary>
/// MiniFASNetV2 (2.7_80x80) anti-spoof via ONNX Runtime. Port of HeyMac's AntiSpoofClassifier.
///
/// Preprocessing reproduced from Core ML / upstream test.py:
///   1. Grow the detector box ~2.7x (Vision box on Mac is ~12% larger, so 2.4 here),
///      clamped/shifted into the frame
///   2. cv2-style bilinear resize to 80x80 (half-pixel-center mapping)
///   3. NCHW float tensor, BGR channel order, raw 0-255 (no scaling — baked into model)
///   4. 3-class output; class index 1 = live
///
/// `modelPath` should point to an AntiSpoof model converted from .mlmodel to ONNX
/// (see PORT_GUIDE.md). Input name "input_image", output "probabilities".
/// </summary>
public sealed class OnnxAntiSpoofClassifier : IAntiSpoofClassifier, IDisposable
{
    private const int InputSize = 80;
    private const int LiveClassIndex = 1;
    private const float VisionBoxScale = 2.4f;

    private readonly Microsoft.ML.OnnxRuntime.InferenceSession _session;
    private readonly string _inputName = "input_image";
    private readonly string _outputName = "probabilities";

    public OnnxAntiSpoofClassifier(string modelPath)
    {
        if (!System.IO.File.Exists(modelPath))
            throw new FileNotFoundException("Anti-spoof ONNX model not found", modelPath);

        var options = new SessionOptions();
        options.AppendExecutionProvider_CPU(0);
        _session = new InferenceSession(modelPath, options);
    }

    public LivenessResult Classify(DetectedFace face, Bitmap frame)
    {
        Rectangle region = FaceGeometry.AntiSpoofCropRect(
            face.BoundingBox, frame.Width, frame.Height, VisionBoxScale);
        Bitmap patch = FaceGeometry.Resize(frame, region, InputSize, InputSize);
        try
        {
            return ClassifyPatch(patch);
        }
        finally
        {
            patch.Dispose();
        }
    }

    public LivenessResult ClassifyPatch(Bitmap patch)
    {
        if (patch.Width != InputSize || patch.Height != InputSize)
            throw new VerificationPipelineException("anti-spoof patch must be 80x80");

        // NCHW, BGR, raw 0-255. channelOrder {0,1,2} keeps GDI+ B,G,R as B,G,R.
        var input = TensorHelper.FromBitmap(
            patch, TensorHelper.Layout.NCHW, new[] { 0, 1, 2 }, normalize: false);

        using var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, input) };
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);

        var output = results.FirstOrDefault(r => r.Name == _outputName)
            ?.AsTensor<float>()
            ?? throw new VerificationPipelineException("anti-spoof output tensor missing");

        if (output.Length != 3)
            throw new VerificationPipelineException("anti-spoof output must have 3 classes");

        float[] probs = new float[3];
        int i = 0;
        foreach (float v in output) probs[i++] = v;

        int predicted = 0;
        for (int j = 1; j < 3; j++)
            if (probs[j] > probs[predicted]) predicted = j;

        return new LivenessResult(predicted == LiveClassIndex, probs[LiveClassIndex]);
    }

    public void Dispose() => _session.Dispose();
}
