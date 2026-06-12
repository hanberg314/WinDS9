namespace WinDS9.Engine;

public sealed record LoadedImage(
    string FilePath,
    string SourceKind,
    int HduIndex,
    int Width,
    int Height,
    float[] Pixels,
    long SourceRows,
    double DataMin,
    double DataMax,
    double LowCut,
    double HighCut,
    TimeSpan LoadDuration,
    int SourceWidth = 0,
    int SourceHeight = 0,
    int BlockFactor = 1,
    long BinnedRows = 0,
    string BinDescription = "",
    WcsMetadata? Wcs = null,
    int CubeDepth = 1,
    int PlaneIndex = 0,
    IReadOnlyList<string>? HeaderCards = null);
