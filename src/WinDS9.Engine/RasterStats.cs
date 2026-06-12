namespace WinDS9.Engine;

public sealed record RasterStats(double Min, double Max, double LowCut, double HighCut)
{
    public static RasterStats Compute(float[] pixels)
    {
        if (pixels.Length == 0)
        {
            return new RasterStats(0, 0, 0, 1);
        }

        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        foreach (var value in pixels)
        {
            if (!float.IsFinite(value))
            {
                continue;
            }

            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        if (!double.IsFinite(min) || !double.IsFinite(max))
        {
            return new RasterStats(0, 0, 0, 1);
        }

        var sample = SamplePixels(pixels, 250_000);
        Array.Sort(sample);
        var zscale = ZScaleEstimator.Estimate(sample);
        var low = zscale?.Low ?? Percentile(sample, 0.005);
        var high = zscale?.High ?? Percentile(sample, 0.995);

        if (high <= low)
        {
            low = min;
            high = max <= min ? min + 1 : max;
        }

        return new RasterStats(min, max, low, high);
    }

    private static float[] SamplePixels(float[] pixels, int maxSamples)
    {
        if (pixels.Length <= maxSamples)
        {
            return pixels.Where(float.IsFinite).ToArray();
        }

        var stride = Math.Max(1, pixels.Length / maxSamples);
        var sample = new List<float>(maxSamples);
        for (var i = 0; i < pixels.Length; i += stride)
        {
            if (float.IsFinite(pixels[i]))
            {
                sample.Add(pixels[i]);
            }
        }

        return sample.ToArray();
    }

    private static double Percentile(float[] sorted, double percentile)
    {
        if (sorted.Length == 0)
        {
            return 0;
        }

        var index = Math.Clamp((int)Math.Round((sorted.Length - 1) * percentile), 0, sorted.Length - 1);
        return sorted[index];
    }
}
