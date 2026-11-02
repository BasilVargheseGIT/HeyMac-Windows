namespace HeyMac.Core.Recognition;

/// <summary>
/// Result of the anti-spoof (liveness) classifier. Mirrors HeyMac's LivenessResult.
/// </summary>
public sealed class LivenessResult
{
    public bool IsLive { get; }
    public float Confidence { get; }

    public LivenessResult(bool isLive, float confidence)
    {
        IsLive = isLive;
        Confidence = confidence;
    }
}
