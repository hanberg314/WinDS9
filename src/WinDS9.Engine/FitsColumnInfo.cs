namespace WinDS9.Engine;

public sealed record FitsColumnInfo(
    int Index,
    string Name,
    string Format,
    int Offset,
    int ByteSize,
    double? Min,
    double? Max,
    double BinSize,
    double Scale,
    double Zero)
{
    public string TrimmedName => Name.Trim();
}
