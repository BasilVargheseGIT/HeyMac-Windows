namespace HeyMac.Engine;

/// <summary>
/// Runtime settings for the HeyMac engine. Mirrors HeyMac's EngineSettings.
/// </summary>
public sealed class EngineSettings
{
    public bool SetupComplete { get; set; }
    public bool Paused { get; set; }
    public bool LockScreenNeedsPassword { get; set; }
    public VerificationStrictness Strictness { get; set; } = VerificationStrictness.Normal;

    public TimeSpan LockScreenScanTimeout { get; set; } = TimeSpan.FromSeconds(6);
    public int MaxScansPerEpisode { get; set; } = 2;
    public double NoMatchCooldownSeconds { get; set; } = 3.5;
    public double NoFaceCooldownSeconds { get; set; } = 1.0;
    public double PollIntervalSeconds { get; set; } = 0.25;
}

public enum VerificationStrictness { Normal, Strict }
