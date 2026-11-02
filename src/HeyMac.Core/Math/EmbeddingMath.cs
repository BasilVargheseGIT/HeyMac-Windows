namespace HeyMac.Core.Math;

/// <summary>
/// Port of HeyMac's EmbeddingMath (Swift).
///
/// Cosine similarity between two L2-normalized ArcFace embeddings. HeyMac's shipped
/// thresholds: matchThreshold = 0.5, livenessThreshold = 0.6. Same-person scores sit
/// well above 0.5; different people well below 0.3.
/// </summary>
public static class EmbeddingMath
{
    public static float CosineSimilarity(FaceEmbedding a, FaceEmbedding b)
    {
        if (a.Vector.Count != b.Vector.Count)
            throw new ArgumentException("embeddings must share the same dimension");

        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Vector.Count; i++)
        {
            float av = a.Vector[i], bv = b.Vector[i];
            dot += av * bv;
            normA += av * av;
            normB += bv * bv;
        }

        if (normA <= 0 || normB <= 0) return 0;
        return dot / ((float)Math.Sqrt(normA) * (float)Math.Sqrt(normB));
    }

    public static FaceEmbedding Normalize(FaceEmbedding embedding)
    {
        float norm = (float)Math.Sqrt(embedding.Vector.Sum(v => v * v));
        if (norm <= 0) return embedding;
        return new FaceEmbedding(embedding.Vector.Select(v => v / norm));
    }

    public static FaceEmbedding Centroid(IReadOnlyList<FaceEmbedding> embeddings)
    {
        if (embeddings.Count == 0)
            throw new ArgumentException("centroid requires at least one embedding");

        int dimension = embeddings[0].Vector.Count;
        float[] sum = new float[dimension];
        foreach (var e in embeddings)
        {
            if (e.Vector.Count != dimension)
                throw new ArgumentException("all embeddings must share a dimension");
            for (int i = 0; i < dimension; i++) sum[i] += e.Vector[i];
        }

        float count = embeddings.Count;
        return new FaceEmbedding(sum.Select(s => s / count));
    }
}
