namespace HeyMac.Engine;

/// <summary>
/// A source of camera frames with a monotonically increasing sequence number.
/// HeyMac used its own FrameSource protocol backed by the Mac camera.
/// </summary>
public interface IFrameSource : IDisposable
{
    void Start();
    void Stop();
    /// <summary>Returns the newest frame with sequence > `newerThan`, or null if none.</summary>
    CameraFrame? Frame(int newerThan);
}

public sealed class CameraFrame
{
    public int Sequence { get; init; }
    public System.Drawing.Bitmap Image { get; init; } = null!;
    public DateTime CapturedAt { get; init; }
}
