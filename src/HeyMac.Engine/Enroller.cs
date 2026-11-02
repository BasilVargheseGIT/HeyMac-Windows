namespace HeyMac.Engine;

using HeyMac.Core.Math;
using HeyMac.Core.Models;
using HeyMac.Core.Recognition;

public enum EnrollmentProgressKind
{
    SampleCaptured,
    RejectedNotLive,
    Finished,
    Failed
}

public sealed class EnrollmentProgress
{
    public EnrollmentProgressKind Kind { get; init; }
    public int Count { get; init; }
    public int Target { get; init; }
    public float Liveness { get; init; }
    public float MinAgreement { get; init; }
    public float MeanAgreement { get; init; }
    public string? Failure { get; init; }
}

/// <summary>
/// Collects `target` spaced-out live samples from the camera and saves their centroid
/// as the enrolled face. Frames without a face are skipped; frames that fail liveness
/// are reported so the UI can explain what to change. Port of HeyMac's Enroller.
/// </summary>
public sealed class Enroller : IDisposable
{
    private readonly IFrameSource _frames;
    private readonly IVerificationPipeline _sampler;
    private readonly object _sessionLock = new();
    public int Target { get; init; } = 8;
    public TimeSpan SampleSpacing { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(40);

    public Enroller(IFrameSource frames, IVerificationPipeline sampler)
    {
        _frames = frames;
        _sampler = sampler;
    }

    public bool Run(Func<bool>? shouldContinue = null, Action<EnrollmentProgress>? onProgress = null)
    {
        shouldContinue ??= () => true;
        onProgress ??= _ => { };

        if (!Monitor.TryEnter(_sessionLock))
        {
            onProgress(new EnrollmentProgress { Kind = EnrollmentProgressKind.Failed,
                Failure = "The camera is busy with another check. Try again in a moment." });
            return false;
        }

        try
        {
            try { _frames.Start(); }
            catch (Exception ex)
            {
                onProgress(new EnrollmentProgress { Kind = EnrollmentProgressKind.Failed,
                    Failure = $"Camera unavailable: {ex.Message}" });
                return false;
            }

            var samples = new List<FaceEmbedding>();
            int lastSequence = 0;
            DateTime lastSampleAt = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));
            DateTime lastLivenessReport = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));
            DateTime deadline = DateTime.UtcNow.Add(Timeout);

            while (samples.Count < Target)
            {
                if (!shouldContinue())
                {
                    onProgress(new EnrollmentProgress { Kind = EnrollmentProgressKind.Failed, Failure = "Enrollment cancelled." });
                    return false;
                }

                DateTime now = DateTime.UtcNow;
                if (now >= deadline)
                {
                    onProgress(new EnrollmentProgress { Kind = EnrollmentProgressKind.Failed,
                        Failure = $"Captured {samples.Count} of {Target} samples in {(int)Timeout.TotalSeconds}s. " +
                        "Check the lighting, keep your face centered, and try again." });
                    return false;
                }

                if (now - lastSampleAt < SampleSpacing) { Thread.Sleep(30); continue; }

                CameraFrame? frame = _frames.Frame(lastSequence);
                if (frame == null) { Thread.Sleep(30); continue; }
                lastSequence = frame.Sequence;

                try
                {
                    var (embedding, liveness) = _sampler.EnrollmentSample(frame.Image);
                    samples.Add(embedding);
                    lastSampleAt = now;
                    onProgress(new EnrollmentProgress
                    {
                        Kind = EnrollmentProgressKind.SampleCaptured,
                        Count = samples.Count, Target = Target, Liveness = liveness.Confidence
                    });
                }
                catch (VerificationPipelineException ex) when (ex.LivenessConfidence.HasValue)
                {
                    if (now - lastLivenessReport >= TimeSpan.FromSeconds(2))
                    {
                        lastLivenessReport = now;
                        onProgress(new EnrollmentProgress
                        {
                            Kind = EnrollmentProgressKind.RejectedNotLive,
                            Liveness = ex.LivenessConfidence.Value
                        });
                    }
                    Thread.Sleep(30);
                }
                catch
                {
                    Thread.Sleep(30);
                }
                finally { frame.Image.Dispose(); }
            }

            if (!shouldContinue())
            {
                onProgress(new EnrollmentProgress { Kind = EnrollmentProgressKind.Failed, Failure = "Enrollment cancelled." });
                return false;
            }

            try
            {
                FaceEmbedding centroid = _sampler.SaveEnrollment(samples);
                var agreement = samples.Select(s => EmbeddingMath.CosineSimilarity(centroid, s)).ToList();
                onProgress(new EnrollmentProgress
                {
                    Kind = EnrollmentProgressKind.Finished,
                    MinAgreement = agreement.Min() ?? 0,
                    MeanAgreement = agreement.Average()
                });
                return true;
            }
            catch (Exception ex)
            {
                onProgress(new EnrollmentProgress { Kind = EnrollmentProgressKind.Failed,
                    Failure = $"Saving enrollment failed: {ex.Message}" });
                return false;
            }
        }
        finally
        {
            _frames.Stop();
            Monitor.Exit(_sessionLock);
        }
    }

    public void Dispose() { }
}
