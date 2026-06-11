namespace Anthology.Modules.Recommendations;

public static class VectorMath
{
    public static float CosineDistance(float[] a, float[] b)
    {
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

        var similarity = dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        return (float)(1 - similarity);
    }
}
