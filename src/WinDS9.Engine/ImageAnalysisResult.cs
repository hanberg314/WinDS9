namespace WinDS9.Engine;

public sealed record ImageStatistics(
    long Count,
    double Min,
    double Max,
    double Mean,
    double Median,
    double StandardDeviation,
    double Sum);

public sealed record HistogramBin(double Low, double High, int Count);

public sealed record ImageAnalysisResult(ImageStatistics Statistics, IReadOnlyList<HistogramBin> Histogram)
{
    public string Summary =>
        $"n={Statistics.Count:N0}, min={Statistics.Min:0.###}, max={Statistics.Max:0.###}, mean={Statistics.Mean:0.###}, median={Statistics.Median:0.###}, sigma={Statistics.StandardDeviation:0.###}";
}
