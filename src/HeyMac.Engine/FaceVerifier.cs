using HeyMac.Core.Security;

namespace HeyMac.Engine;

/// <summary>
/// Runs a bounded verification window over live camera frames. A match needs
/// `requiredConsecutive` consecutive frames that are both live and above the similarity
/// threshold, so one lucky frame can't unlock. `sessionLock` ensures only one window
/// or enrollment runs at a time.
/// Port of HeyMac's FaceVerifier.
/// </summary>
public sealed class FaceVerifier : IFaceMatcher, IDisposable
{
    public const int MismatchFrames = 6;

    private readonly IFrameSource _camera;
    private readonly IVerificationPipeline _pipeline;
    private readonly object _sessionLock = new();

    public FaceVerifier(IFrameSource camera, IVerificationPipeline pipeline)
    {
        _camera = camera;
        _pipeline = pipeline;
    }

    public VerificationOutcome Run(TimeSpan timeout, int requiredConsecutive = 2,
        TimeSpan waitForTurn = default, Func<bool>? keepGoing = null)
    {
        var outcome = new VerificationOutcome { MatchThreshold = _pipeline.Config.MatchThreshold };
        keepGoing ??= () => true;

        // Serialize access to the camera so a verification and enrollment can't overlap.
        bool gotLock = Monitor.TryEnter(_sessionLock, (int)waitForTurn.TotalMilliseconds);
        if (!gotLock)
        {
            outcome.Failure = "camera busy with another verification";
            return outcome;
        }

        try
        {
            // Load enrollment before touching the camera: no enrollment fails instantly
            // without flashing the camera light.
            FaceEmbedding centroid;
            try
            {
                centroid = _pipeline.LoadEnrolledCentroid();
            }
            catch (SecureStoreException ex) when (ex.Code == SecureStoreError.NotFound)
            {
                outcome.Failure = "not enrolled";
                return outcome;
            }
            catch (VerificationPipelineException ex)
            {
                outcome.Failure = ex.Message;
                return outcome;
            }
            catch (Exception ex)
            {
                outcome.Failure = $"enrollment unavailable: {ex.Message}";
                return outcome;
            }

            DateTime deadline = DateTime.UtcNow.Add(timeout);
            try { _camera.Start(); }
            catch (Exception ex)
            {
                outcome.Failure = $"camera unavailable: {ex.Message}";
                return outcome;
            }

            int lastSequence = 0;
            int consecutive = 0;
            int wrongStreak = 0;

            while (DateTime.UtcNow < deadline && keepGoing())
            {
                CameraFrame? frame = _camera.Frame(lastSequence);
                if (frame == null)
                {
                    Thread.Sleep(30);
                    continue;
                }
                lastSequence = frame.Image != null ? lastSequence + 1 : lastSequence;

                try
                {
                    FrameEvaluation eval = _pipeline.Evaluate(frame.Image, centroid);
                    outcome.FramesEvaluated++;
                    outcome.BestSimilarity = Math.Max(outcome.BestSimilarity, eval.Similarity);
                    outcome.BestLiveness = Math.Max(outcome.BestLiveness, eval.Liveness);

                    consecutive = eval.Accepted ? consecutive + 1 : 0;
                    wrongStreak = eval.IsMatch ? 0 : wrongStreak + 1;
                    if (!eval.IsMatch) outcome.WrongFaceFrames++;
                    outcome.LongestWrongStreak = Math.Max(outcome.LongestWrongStreak, wrongStreak);

                    if (consecutive >= requiredConsecutive)
                    {
                        outcome.Matched = true;
                        return outcome;
                    }
                    if (wrongStreak >= MismatchFrames)
                    {
                        outcome.Mismatched = true;
                        return outcome;
                    }
                }
                catch
                {
                    outcome.FramesWithoutFace++;
                    consecutive = 0;
                    wrongStreak = 0;
                }
                finally
                {
                    frame.Image.Dispose();
                }
            }
        }
        finally
        {
            _camera.Stop();
            Monitor.Exit(_sessionLock);
        }

        return outcome;
    }

    public void Dispose() { }
}

/// <summary>
/// A face matcher that runs a bounded verification window.
/// </summary>
public interface IFaceMatcher : IDisposable
{
    VerificationOutcome Run(TimeSpan timeout, int requiredConsecutive = 2,
        TimeSpan waitForTurn = default, Func<bool>? keepGoing = null);
}
