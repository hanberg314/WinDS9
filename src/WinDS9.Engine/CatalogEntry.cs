namespace WinDS9.Engine;

public enum CatalogCoordinateSystem
{
    Image,
    Sky
}

public sealed record CatalogEntry(
    double ImageX,
    double ImageY,
    string? Label = null,
    CatalogCoordinateSystem CoordinateSystem = CatalogCoordinateSystem.Image,
    string SkyFrame = "fk5",
    IReadOnlyDictionary<string, string>? Columns = null)
{
    public double First => ImageX;

    public double Second => ImageY;
}
