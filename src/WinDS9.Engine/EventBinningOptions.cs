namespace WinDS9.Engine;

public sealed record EventBinningOptions(
    string? XColumnName = null,
    string? YColumnName = null,
    int? BlockFactor = null,
    int MaxRasterSide = 2048,
    int BufferSize = 1024,
    double? CenterX = null,
    double? CenterY = null,
    EventBinningMode Mode = EventBinningMode.FullExtent,
    EventBinFunction Function = EventBinFunction.Sum);
