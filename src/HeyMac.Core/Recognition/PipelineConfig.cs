namespace HeyMac.Core.Recognition;

/// <summary>
/// Configuration for the verification pipeline. Matches HeyMac's PipelineConfig defaults.
/// </summary>
public sealed class PipelineConfig
{
    /// <summary>Cosine similarity threshold for a match (HeyMac default 0.5).</summary>
    public float MatchThreshold { get; set; } = 0.5f;

    /// <summary>Minimum anti-spoof probability for the live class (HeyMac default 0.6).</summary>
    public float LivenessThreshold { get; set; } = 0.6f;

    public PipelineConfig() { }
    public PipelineConfig(float matchThreshold, float livenessThreshold)
    {
        MatchThreshold = matchThreshold;
        LivenessThreshold = livenessThreshold;
    }
}
