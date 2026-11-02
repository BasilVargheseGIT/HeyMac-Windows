namespace HeyMac.Core.Recognition;

/// <summary>
/// Result of evaluating a single frame against the enrolled centroid.
/// </summary>
public sealed class FrameEvaluation
{
    public float Liveness { get; }
    public float Similarity { get; }
    public bool IsLive { get; }
    public bool IsMatch { get; }
    public bool Accepted => IsLive && IsMatch;

    public FrameEvaluation(float liveness, float similarity, bool isLive, bool isMatch)
    {
        Liveness = liveness;
        Similarity = similarity;
        IsLive = isLive;
        IsMatch = isMatch;
    }
}
