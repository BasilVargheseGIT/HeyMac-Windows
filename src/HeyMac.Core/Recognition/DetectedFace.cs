using System.Drawing;

namespace HeyMac.Core.Recognition;

/// <summary>
/// A face located in a frame, in pixel coordinates (top-left origin, y grows down),
/// with the 5 ArcFace alignment landmarks in template order:
/// image-left eye, image-right eye, nose tip, image-left mouth corner, image-right mouth corner.
/// </summary>
public sealed class DetectedFace
{
    public Rectangle BoundingBox { get; }
    public IReadOnlyList<PointF> Landmarks { get; }

    public DetectedFace(Rectangle boundingBox, IReadOnlyList<PointF> landmarks)
    {
        BoundingBox = boundingBox;
        Landmarks = landmarks;
    }
}
