namespace Anthology.Modules.Recommendations;

public static class VectorMath
{
    public static float CosineDistance(float[] a, float[] b)
    {
        // Degenerate inputs (length mismatch, empty, or zero-magnitude) return 1f — "unrelated/neutral distance".
        // Guarded rather than thrown so the diversity guard never crashes on bad data.
        if (a.Length != b.Length || a.Length == 0)
            return 1f;

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
            return 1f;

        var similarity = dot / Math.Sqrt(normA * normB);
        return (float)Math.Clamp(1.0 - similarity, 0.0, 2.0);
    }
}
