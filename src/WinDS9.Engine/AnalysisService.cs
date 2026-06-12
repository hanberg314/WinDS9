namespace WinDS9.Engine;

public sealed class AnalysisService
{
    public ImageAnalysisResult Analyze(LoadedImage image, int histogramBins = 64)
    {
        var finite = image.Pixels.Where(float.IsFinite).Select(value => (double)value).ToArray();
        if (finite.Length == 0)
        {
            var empty = new ImageStatistics(0, 0, 0, 0, 0, 0, 0);
            return new ImageAnalysisResult(empty, []);
        }

        Array.Sort(finite);
        var min = finite[0];
        var max = finite[^1];
        var sum = finite.Sum();
        var mean = sum / finite.Length;
        var variance = finite.Length > 1
            ? finite.Sum(value => (value - mean) * (value - mean)) / (finite.Length - 1)
            : 0;
        var median = finite.Length % 2 == 0
            ? (finite[finite.Length / 2 - 1] + finite[finite.Length / 2]) / 2
            : finite[finite.Length / 2];

        var stats = new ImageStatistics(
            finite.Length,
            min,
            max,
            mean,
            median,
            Math.Sqrt(variance),
            sum);

        return new ImageAnalysisResult(stats, BuildHistogram(finite, min, max, histogramBins));
    }

    private static IReadOnlyList<HistogramBin> BuildHistogram(double[] sorted, double min, double max, int binCount)
    {
        binCount = Math.Clamp(binCount, 1, 512);
        if (max <= min)
        {
            return [new HistogramBin(min, max, sorted.Length)];
        }

        var counts = new int[binCount];
        var range = max - min;
        foreach (var value in sorted)
        {
            var index = Math.Clamp((int)((value - min) / range * binCount), 0, binCount - 1);
            counts[index]++;
        }

        var bins = new List<HistogramBin>(binCount);
        for (var i = 0; i < counts.Length; i++)
        {
            var low = min + range * i / binCount;
            var high = min + range * (i + 1) / binCount;
            bins.Add(new HistogramBin(low, high, counts[i]));
        }

        return bins;
    }
}
