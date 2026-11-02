namespace HeyMac.Engine;

/// <summary>
/// Outcome of a bounded verification window over live camera frames.
/// Port of HeyMac's VerificationOutcome.
/// </summary>
public sealed class VerificationOutcome
{
    public bool Matched { get; set; }
    public int FramesEvaluated { get; set; }
    public int FramesWithoutFace { get; set; }
    public float BestSimilarity { get; set; } = -1f;
    public float BestLiveness { get; set; }
    public string? Failure { get; set; }
    public bool Mismatched { get; set; }
    public int WrongFaceFrames { get; set; }
    public int LongestWrongStreak { get; set; }
    public float MatchThreshold { get; set; }

    public string DebugSummary =>
        $"faceFrames={FramesEvaluated} noFaceFrames={FramesWithoutFace} wrongFaceFrames={WrongFaceFrames} " +
        $"longestWrongStreak={LongestWrongStreak}/{FaceVerifier.MismatchFrames} " +
        $"bestSimilarity={BestSimilarity:F3} threshold={MatchThreshold:F2} bestLiveness={BestLiveness:F3}";

    public string Summary =>
        Failure != null ? $"FAIL ({Failure})"
        : $"{(Matched ? "OK " : "FAIL ")}frames={FramesEvaluated} noFace={FramesWithoutFace} " +
          $"bestSimilarity={BestSimilarity:F3} bestLiveness={BestLiveness:F3}";
}
