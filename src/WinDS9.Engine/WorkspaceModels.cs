namespace WinDS9.Engine;

public sealed record ViewportState(
    double Zoom = 1,
    double CenterImageX = 0,
    double CenterImageY = 0,
    bool FitToViewport = true);

public enum FrameLayerKind
{
    Region,
    Catalog,
    Contour
}

public sealed record FrameLayer(
    FrameLayerKind Kind,
    string Name,
    bool IsVisible = true,
    IReadOnlyList<Ds9Region>? Regions = null,
    IReadOnlyList<CatalogEntry>? CatalogEntries = null,
    IReadOnlyList<ContourSegment>? Contours = null);

public sealed record FrameDocument(
    string FilePath,
    int HduIndex,
    int PlaneIndex,
    LoadedImage Image,
    ViewportState Viewport,
    IReadOnlyList<FrameLayer> Layers);
