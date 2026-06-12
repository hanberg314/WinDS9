namespace WinDS9.Engine;

internal static class Ds9TableCoordinateMath
{
    public static int ToImageIndex(double physicalValue, double min, double binSize)
    {
        var index = binSize is 1.0 or <= 0.0
            ? physicalValue - min + 1.0
            : (physicalValue - min) / binSize + 1.0;

        return (int)index;
    }

    public static int Dimension(double min, double max, double binSize, char columnType)
    {
        var width = UpperEdge(max, columnType) - LowerEdge(min, columnType);
        var dimension = binSize is 1.0 or <= 0.0
            ? width
            : width / binSize;

        return Math.Max(0, (int)Math.Round(dimension, MidpointRounding.AwayFromZero));
    }

    public static double LowerEdge(double min, char columnType)
    {
        return IsIntegerColumn(columnType) ? min - 0.5 : min;
    }

    public static double UpperEdge(double max, char columnType)
    {
        return IsIntegerColumn(columnType) ? max + 0.5 : max;
    }

    private static bool IsIntegerColumn(char columnType)
    {
        return char.ToUpperInvariant(columnType) is not ('E' or 'D');
    }
}
