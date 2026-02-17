namespace SupportAgent.Api.AI
{
    public static class VectorMath
    {
        public static double CosineSimilarity(float[] a, float[] b)
        {
            double dot = 0;
            double normA = 0;
            double normB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            return dot / (Math.Sqrt(normA) * Math.Sqrt(normB) + 1e-10); // Add small value to prevent division by zero
        }
    }
}
