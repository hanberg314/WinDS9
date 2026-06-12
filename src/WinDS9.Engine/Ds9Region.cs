namespace WinDS9.Engine;

public enum Ds9RegionKind
{
    Circle,
    Box,
    Ellipse,
    Point,
    Line,
    Polygon,
    Annulus,
    Text,
    Ruler,
    Vector,
    Segment,
    Projection
}

public sealed record Ds9Region(
    Ds9RegionKind Kind,
    IReadOnlyList<double> Values,
    string CoordinateSystem,
    string? Label = null)
{
    public bool IsImageLike =>
        string.Equals(CoordinateSystem, "image", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(CoordinateSystem, "physical", StringComparison.OrdinalIgnoreCase);

    public bool IsWorldLike =>
        string.Equals(CoordinateSystem, "fk5", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(CoordinateSystem, "icrs", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(CoordinateSystem, "galactic", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(CoordinateSystem, "ecliptic", StringComparison.OrdinalIgnoreCase);
}
