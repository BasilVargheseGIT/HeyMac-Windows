namespace HeyMac.Core.Models;

/// <summary>
/// A face embedding vector produced by the ArcFace model, L2-normalized.
/// HeyMac stores the enrolled identity as the normalized centroid of several samples.
/// </summary>
public sealed class FaceEmbedding
{
    public IReadOnlyList<float> Vector { get; }

    public FaceEmbedding(IEnumerable<float> vector)
    {
        Vector = vector.ToList().AsReadOnly();
    }

    public int Dimension => Vector.Count;
}
