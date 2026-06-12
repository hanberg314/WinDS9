namespace WinDS9.Engine;

public sealed record FitsHduInfo(
    int Index,
    string ExtensionType,
    string? ExtensionName,
    long HeaderOffset,
    long DataOffset,
    long DataByteCount,
    int BitPix,
    int NAxis,
    IReadOnlyList<int> Axes,
    int RowByteCount,
    int RowCount,
    IReadOnlyList<FitsColumnInfo> Columns,
    FitsHeader Header)
{
    public bool IsImage => NAxis >= 2 && Axes.Count >= 2 && DataByteCount > 0 && !string.Equals(ExtensionType, "BINTABLE", StringComparison.OrdinalIgnoreCase);
    public bool IsBinaryTable => string.Equals(ExtensionType, "BINTABLE", StringComparison.OrdinalIgnoreCase);
}
