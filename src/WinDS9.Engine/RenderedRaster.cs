namespace WinDS9.Engine;

public sealed record RenderedRaster(
    int Width,
    int Height,
    byte[] Bgra32,
    double LowCut,
    double HighCut,
    ImageStretch Stretch,
    ImageColorMap ColorMap = ImageColorMap.Gray);
