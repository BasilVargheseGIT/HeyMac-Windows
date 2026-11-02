namespace HeyMac.Engine.Camera;

using OpenCvSharp;

/// <summary>
/// OpenCVSharp-backed camera frame source. Port of HeyMac's FrameSource protocol.
/// Reads frames from a webcam and hands them off one at a time with sequence numbers.
/// </summary>
public sealed class CameraFrameSource : IFrameSource
{
    private readonly VideoCapture _capture = new();
    private readonly int _deviceIndex;
    private int _sequence;
    private bool _running;

    public CameraFrameSource(int deviceIndex = 0)
    {
        _deviceIndex = deviceIndex;
    }

    public void Start()
    {
        if (_running) return;
        if (!_capture.Open(_deviceIndex))
            throw new System.Exception($"Could not open webcam device {_deviceIndex}");
        _running = true;
    }

    public void Stop()
    {
        if (!_running) return;
        _capture.Release();
        _running = false;
    }

    public CameraFrame? Frame(int newerThan)
    {
        if (!_running) return null;
        using Mat mat = new();
        if (!_capture.Read(mat) || mat.Empty()) return null;

        int sequence = Interlocked.Increment(ref _sequence);
        if (sequence <= newerThan) return null;

        return new CameraFrame
        {
            Sequence = sequence,
            Image = mat.ToBitmap(),
            CapturedAt = DateTime.UtcNow
        };
    }

    public void Dispose() => Stop();
}
