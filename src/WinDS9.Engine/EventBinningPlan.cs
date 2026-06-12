namespace WinDS9.Engine;

public sealed record EventBinningPlan(
    FitsColumnInfo XColumn,
    FitsColumnInfo YColumn,
    double XMin,
    double XMax,
    double YMin,
    double YMax,
    double XBinSize,
    double YBinSize,
    double XLower,
    double XUpper,
    double YLower,
    double YUpper,
    double CenterX,
    double CenterY,
    double OutputCenterX,
    double OutputCenterY,
    int SourceWidth,
    int SourceHeight,
    int X0,
    int X1,
    int Y0,
    int Y1,
    int BlockFactor,
    int OutputWidth,
    int OutputHeight,
    EventBinningMode Mode,
    EventBinFunction Function)
{
    public string BinColumns => $"bincols=({XColumn.TrimmedName},{YColumn.TrimmedName})";
}
