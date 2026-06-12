namespace WinDS9.Engine;

public enum Ds9CommandKind
{
    Unknown,
    Open,
    Frame,
    Scale,
    ColorMap,
    Zoom,
    Pan,
    Region,
    Catalog,
    Contour
}

public sealed record Ds9Command(Ds9CommandKind Kind, string Name, IReadOnlyList<string> Arguments);
