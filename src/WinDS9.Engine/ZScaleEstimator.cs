namespace WinDS9.Engine;

public static class ZScaleEstimator
{
    public static (double Low, double High)? Estimate(
        IReadOnlyList<float> pixels,
        int maxSamples = 10_000,
        double contrast = 0.25)
    {
        if (pixels.Count == 0)
        {
            return null;
        }

        var sample = SampleFinitePixels(pixels, maxSamples);
        if (sample.Length < 8)
        {
            return null;
        }

        Array.Sort(sample);
        var min = sample[0];
        var max = sample[^1];
        if (max <= min)
        {
            return (min, min + 1);
        }

        var median = sample[sample.Length / 2];
        var fitted = FitClippedLine(sample);
        if (fitted is null || fitted.Value.Slope <= 0 || !double.IsFinite(fitted.Value.Slope))
        {
            return null;
        }

        var center = (sample.Length - 1) / 2.0;
        var slope = fitted.Value.Slope / Math.Max(contrast, 1e-6);
        var low = median - center * slope;
        var high = median + (sample.Length - 1 - center) * slope;

        low = Math.Clamp(low, min, max);
        high = Math.Clamp(high, min, max);
        return high > low ? (low, high) : null;
    }

    private static float[] SampleFinitePixels(IReadOnlyList<float> pixels, int maxSamples)
    {
        var stride = Math.Max(1, pixels.Count / Math.Max(1, maxSamples));
        var sample = new List<float>(Math.Min(maxSamples, pixels.Count));
        for (var i = 0; i < pixels.Count; i += stride)
        {
            var value = pixels[i];
            if (float.IsFinite(value))
            {
                sample.Add(value);
            }
        }

        return sample.ToArray();
    }

    private static (double Intercept, double Slope)? FitClippedLine(float[] sorted)
    {
        var keep = new bool[sorted.Length];
        Array.Fill(keep, true);
        (double Intercept, double Slope) fit = default;
        var kept = sorted.Length;

        for (var iteration = 0; iteration < 5; iteration++)
        {
            fit = FitLine(sorted, keep, kept);
            var sigma = ResidualSigma(sorted, keep, fit);
            if (!double.IsFinite(sigma) || sigma <= 0)
            {
                break;
            }

            var threshold = 2.5 * sigma;
            var nextKept = 0;
            for (var i = 0; i < sorted.Length; i++)
            {
                if (!keep[i])
                {
                    continue;
                }

                var x = i - (sorted.Length - 1) / 2.0;
                var residual = Math.Abs(sorted[i] - (fit.Intercept + fit.Slope * x));
                keep[i] = residual <= threshold;
                if (keep[i])
                {
                    nextKept++;
                }
            }

            if (nextKept == kept || nextKept < sorted.Length / 2)
            {
                kept = nextKept;
                break;
            }

            kept = nextKept;
        }

        return kept >= sorted.Length / 2 ? fit : null;
    }

    private static (double Intercept, double Slope) FitLine(float[] sorted, bool[] keep, int kept)
    {
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXX = 0.0;
        var sumXY = 0.0;
        var center = (sorted.Length - 1) / 2.0;

        for (var i = 0; i < sorted.Length; i++)
        {
            if (!keep[i])
            {
                continue;
            }

            var x = i - center;
            var y = sorted[i];
            sumX += x;
            sumY += y;
            sumXX += x * x;
            sumXY += x * y;
        }

        var denominator = kept * sumXX - sumX * sumX;
        if (denominator == 0)
        {
            return (sumY / Math.Max(kept, 1), 0);
        }

        var slope = (kept * sumXY - sumX * sumY) / denominator;
        var intercept = (sumY - slope * sumX) / kept;
        return (intercept, slope);
    }

    private static double ResidualSigma(float[] sorted, bool[] keep, (double Intercept, double Slope) fit)
    {
        var center = (sorted.Length - 1) / 2.0;
        var residuals = new List<double>();
        for (var i = 0; i < sorted.Length; i++)
        {
            if (!keep[i])
            {
                continue;
            }

            var x = i - center;
            residuals.Add(sorted[i] - (fit.Intercept + fit.Slope * x));
        }

        if (residuals.Count < 2)
        {
            return 0;
        }

        var mean = residuals.Average();
        var variance = residuals.Sum(value => (value - mean) * (value - mean)) / (residuals.Count - 1);
        return Math.Sqrt(variance);
    }
}
