namespace HeyMac.Engine;

using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;

/// <summary>
/// OpenCVSharp implementation of IFaceDetector.
///
/// Primary: OpenCV's FaceDetectorYN (YuNet) ONNX model, which returns bounding boxes
/// AND five facial landmarks — exactly what ArcFace alignment needs.
///
/// Fallback: Haar-cascade face detection with landmarks estimated from the face box
/// (functional but less accurate; alignment quality depends on real landmarks).
///
/// You need the YuNet model (`face_detection_yunet_2023mar.onnx` from OpenCV Zoo) for
/// landmark-based detection. See PORT_GUIDE.md.
/// </summary>
public sealed class OpenCvFaceDetector : IFaceDetector
{
    private readonly string? _yunetModelPath;
    private readonly CascadeClassifier? _haarCascade;

    public OpenCvFaceDetector(string? yunetModelPath = null, string? haarCascadePath = null)
    {
        _yunetModelPath = yunetModelPath;
        if (!string.IsNullOrEmpty(haarCascadePath) && System.IO.File.Exists(haarCascadePath))
            _haarCascade = new CascadeClassifier(haarCascadePath);
    }

    public DetectedFace DetectLargestFace(Bitmap image)
    {
        using Mat mat = BitmapToMat(image);
        if (_yunetModelPath != null && System.IO.File.Exists(_yunetModelPath))
            return DetectYunet(mat);

        return DetectHaar(mat);
    }

    private static Mat BitmapToMat(Bitmap bitmap)
    {
        using var ms = new System.IO.MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        byte[] bytes = ms.ToArray();
        return Cv2.ImDecode(bytes, ImreadModes.Color);
    }

    private DetectedFace DetectYunet(Mat mat)
    {
        using var detector = FaceDetectorYN.Create(
            _yunetModelPath!, "", new Size(mat.Width, mat.Height),
            scoreThreshold: 0.6f, nmsThreshold: 0.3f, topK: 5000);
        using Mat faces = new();
        detector.Detect(mat, faces);

        int rows = faces.Rows;
        if (rows == 0) throw new VerificationPipelineException("no face detected");

        // Pick largest face by area (col 2 = width, col 3 = height).
        int best = 0; float bestArea = 0;
        for (int r = 0; r < rows; r++)
        {
            float w = faces.At<float>(r, 2), h = faces.At<float>(r, 3);
            float area = w * h;
            if (area > bestArea) { bestArea = area; best = r; }
        }

        float x = faces.At<float>(best, 0), y = faces.At<float>(best, 1),
              w = faces.At<float>(best, 2), h = faces.At<float>(best, 3);
        var box = new Rectangle((int)x, (int)y, (int)w, (int)h);

        // YuNet landmark columns: 4,5 right eye | 6,7 left eye | 8,9 nose | 10,11 right mouth | 12,13 left mouth
        float[] reye = { faces.At<float>(best, 4), faces.At<float>(best, 5) };
        float[] leye = { faces.At<float>(best, 6), faces.At<float>(best, 7) };
        float[] nose = { faces.At<float>(best, 8), faces.At<float>(best, 9) };
        float[] rmouth = { faces.At<float>(best, 10), faces.At<float>(best, 11) };
        float[] lmouth = { faces.At<float>(best, 12), faces.At<float>(best, 13) };

        // ArcFace template order: image-left eye, image-right eye, nose, image-left mouth, image-right mouth.
        // Sort eyes/mouths by X so the leftmost is first.
        PointF eyeLeft = reye[0] <= leye[0] ? new PointF(reye[0], reye[1]) : new PointF(leye[0], leye[1]);
        PointF eyeRight = reye[0] <= leye[0] ? new PointF(leye[0], leye[1]) : new PointF(reye[0], reye[1]);
        PointF mouthLeft = rmouth[0] <= lmouth[0] ? new PointF(rmouth[0], rmouth[1]) : new PointF(lmouth[0], lmouth[1]);
        PointF mouthRight = rmouth[0] <= lmouth[0] ? new PointF(lmouth[0], lmouth[1]) : new PointF(rmouth[0], rmouth[1]);

        return new DetectedFace(box, new List<PointF> { eyeLeft, eyeRight, new PointF(nose[0], nose[1]), mouthLeft, mouthRight });
    }

    private DetectedFace DetectHaar(Mat mat)
    {
        if (_haarCascade == null)
            throw new VerificationPipelineException("no face detector available (provide a YuNet or Haar model)");

        using Mat gray = new();
        Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(gray, gray);

        Rect[] faces = _haarCascade.DetectMultiScale(gray, 1.1, 3, HaarDetectionTypes.ScaleRarely);
        if (faces.Length == 0) throw new VerificationPipelineException("no face detected");

        Rect best = faces[0];
        foreach (var f in faces) if (f.Area > best.Area) best = f;

        // Estimate 5 landmarks from the box (crude fallback).
        float cx = best.Left + best.Width / 2f;
        var landmarks = new List<PointF>
        {
            new PointF(best.Left + best.Width * 0.30f, best.Top + best.Height * 0.35f), // left eye
            new PointF(best.Left + best.Width * 0.70f, best.Top + best.Height * 0.35f), // right eye
            new PointF(cx, best.Top + best.Height * 0.55f),                            // nose
            new PointF(best.Left + best.Width * 0.30f, best.Top + best.Height * 0.75f), // left mouth
            new PointF(best.Left + best.Width * 0.70f, best.Top + best.Height * 0.75f), // right mouth
        };
        return new DetectedFace(best, landmarks);
    }
}
