namespace WinDS9.Engine;

public sealed record WcsMetadata(
    string? CoordinateType1,
    string? CoordinateType2,
    double? ReferencePixel1,
    double? ReferencePixel2,
    double? ReferenceValue1,
    double? ReferenceValue2,
    double? Cd11,
    double? Cd12,
    double? Cd21,
    double? Cd22,
    string? Unit1,
    string? Unit2)
{
    private const double DegToRad = Math.PI / 180;
    private const double RadToDeg = 180 / Math.PI;

    public bool HasCelestialAxes =>
        IsCelestial(CoordinateType1) && IsCelestial(CoordinateType2);

    public bool HasLinearTransform =>
        ReferencePixel1.HasValue &&
        ReferencePixel2.HasValue &&
        ReferenceValue1.HasValue &&
        ReferenceValue2.HasValue &&
        Cd11.HasValue &&
        Cd12.HasValue &&
        Cd21.HasValue &&
        Cd22.HasValue;

    public bool IsTangentProjection =>
        HasCelestialAxes &&
        IsProjection(CoordinateType1, "TAN") &&
        IsProjection(CoordinateType2, "TAN");

    public string Summary
    {
        get
        {
            if (!HasLinearTransform)
            {
                return "none";
            }

            var type1 = string.IsNullOrWhiteSpace(CoordinateType1) ? "X" : CoordinateType1;
            var type2 = string.IsNullOrWhiteSpace(CoordinateType2) ? "Y" : CoordinateType2;
            var projection = IsTangentProjection ? " TAN" : string.Empty;
            return $"{type1}/{type2}{projection} CRVAL=({ReferenceValue1:0.######}, {ReferenceValue2:0.######})";
        }
    }

    public (double World1, double World2)? PixelToWorld(double pixel1, double pixel2)
    {
        if (!HasLinearTransform)
        {
            return null;
        }

        var intermediate = PixelToIntermediate(pixel1, pixel2);
        return IsTangentProjection
            ? IntermediateToTanWorld(intermediate.X, intermediate.Y)
            : (ReferenceValue1!.Value + intermediate.X, ReferenceValue2!.Value + intermediate.Y);
    }

    public (double Pixel1, double Pixel2)? WorldToPixel(double world1, double world2)
    {
        if (!HasLinearTransform)
        {
            return null;
        }

        var intermediate = IsTangentProjection
            ? TanWorldToIntermediate(world1, world2)
            : (X: world1 - ReferenceValue1!.Value, Y: world2 - ReferenceValue2!.Value);

        if (intermediate is null)
        {
            return null;
        }

        return IntermediateToPixel(intermediate.Value.X, intermediate.Value.Y);
    }

    public string FormatWorld(double world1, double world2)
    {
        if (!HasCelestialAxes)
        {
            return $"{AxisLabel(CoordinateType1, "W1")} {world1:0.######}, {AxisLabel(CoordinateType2, "W2")} {world2:0.######}";
        }

        return $"{AxisLabel(CoordinateType1, "RA")} {FormatRightAscension(world1)}, {AxisLabel(CoordinateType2, "Dec")} {FormatDeclination(world2)}";
    }

    public static WcsMetadata? FromHeader(FitsHeader header)
    {
        var cd11 = header.GetDouble("CD1_1");
        var cd12 = header.GetDouble("CD1_2");
        var cd21 = header.GetDouble("CD2_1");
        var cd22 = header.GetDouble("CD2_2");

        if (!cd11.HasValue || !cd12.HasValue || !cd21.HasValue || !cd22.HasValue)
        {
            var cdelt1 = header.GetDouble("CDELT1");
            var cdelt2 = header.GetDouble("CDELT2");
            var pc11 = header.GetDouble("PC1_1", 1);
            var pc12 = header.GetDouble("PC1_2", 0);
            var pc21 = header.GetDouble("PC2_1", 0);
            var pc22 = header.GetDouble("PC2_2", 1);

            if (cdelt1.HasValue && cdelt2.HasValue)
            {
                cd11 ??= pc11 * cdelt1.Value;
                cd12 ??= pc12 * cdelt1.Value;
                cd21 ??= pc21 * cdelt2.Value;
                cd22 ??= pc22 * cdelt2.Value;
            }
        }

        var wcs = new WcsMetadata(
            header.GetString("CTYPE1"),
            header.GetString("CTYPE2"),
            header.GetDouble("CRPIX1"),
            header.GetDouble("CRPIX2"),
            header.GetDouble("CRVAL1"),
            header.GetDouble("CRVAL2"),
            cd11,
            cd12,
            cd21,
            cd22,
            header.GetString("CUNIT1"),
            header.GetString("CUNIT2"));

        return wcs.HasLinearTransform ||
               !string.IsNullOrWhiteSpace(wcs.CoordinateType1) ||
               !string.IsNullOrWhiteSpace(wcs.CoordinateType2)
            ? wcs
            : null;
    }

    private (double X, double Y) PixelToIntermediate(double pixel1, double pixel2)
    {
        var dx = pixel1 - ReferencePixel1!.Value;
        var dy = pixel2 - ReferencePixel2!.Value;
        return (
            Cd11!.Value * dx + Cd12!.Value * dy,
            Cd21!.Value * dx + Cd22!.Value * dy);
    }

    private (double Pixel1, double Pixel2)? IntermediateToPixel(double x, double y)
    {
        var determinant = Cd11!.Value * Cd22!.Value - Cd12!.Value * Cd21!.Value;
        if (Math.Abs(determinant) < 1e-30)
        {
            return null;
        }

        var dx = (Cd22.Value * x - Cd12.Value * y) / determinant;
        var dy = (-Cd21.Value * x + Cd11.Value * y) / determinant;
        return (ReferencePixel1!.Value + dx, ReferencePixel2!.Value + dy);
    }

    private (double World1, double World2) IntermediateToTanWorld(double xDegrees, double yDegrees)
    {
        var xi = xDegrees * DegToRad;
        var eta = yDegrees * DegToRad;
        var ra0 = ReferenceValue1!.Value * DegToRad;
        var dec0 = ReferenceValue2!.Value * DegToRad;
        var cosDec0 = Math.Cos(dec0);
        var sinDec0 = Math.Sin(dec0);
        var denominator = cosDec0 - eta * sinDec0;

        var ra = ra0 + Math.Atan2(xi, denominator);
        var dec = Math.Atan2(
            sinDec0 + eta * cosDec0,
            Math.Sqrt(denominator * denominator + xi * xi));

        return (NormalizeDegrees(ra * RadToDeg), dec * RadToDeg);
    }

    private (double X, double Y)? TanWorldToIntermediate(double world1, double world2)
    {
        var ra = world1 * DegToRad;
        var dec = world2 * DegToRad;
        var ra0 = ReferenceValue1!.Value * DegToRad;
        var dec0 = ReferenceValue2!.Value * DegToRad;
        var deltaRa = NormalizeRadians(ra - ra0);
        var sinDec = Math.Sin(dec);
        var cosDec = Math.Cos(dec);
        var sinDec0 = Math.Sin(dec0);
        var cosDec0 = Math.Cos(dec0);
        var denominator = sinDec * sinDec0 + cosDec * cosDec0 * Math.Cos(deltaRa);
        if (denominator <= 0 || Math.Abs(denominator) < 1e-30)
        {
            return null;
        }

        var xi = cosDec * Math.Sin(deltaRa) / denominator;
        var eta = (sinDec * cosDec0 - cosDec * sinDec0 * Math.Cos(deltaRa)) / denominator;
        return (xi * RadToDeg, eta * RadToDeg);
    }

    private static bool IsCelestial(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.StartsWith("RA", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("DEC", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("GLON", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("GLAT", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProjection(string? value, string projection)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains($"-{projection}", StringComparison.OrdinalIgnoreCase);
    }

    private static string AxisLabel(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Split('-')[0];
    }

    private static string FormatRightAscension(double degrees)
    {
        var hours = NormalizeDegrees(degrees) / 15.0;
        var wholeHours = (int)Math.Floor(hours);
        var minutesFloat = (hours - wholeHours) * 60;
        var minutes = (int)Math.Floor(minutesFloat);
        var seconds = (minutesFloat - minutes) * 60;
        return $"{wholeHours:00}:{minutes:00}:{seconds:00.###}";
    }

    private static string FormatDeclination(double degrees)
    {
        var sign = degrees < 0 ? "-" : "+";
        var abs = Math.Abs(degrees);
        var wholeDegrees = (int)Math.Floor(abs);
        var minutesFloat = (abs - wholeDegrees) * 60;
        var minutes = (int)Math.Floor(minutesFloat);
        var seconds = (minutesFloat - minutes) * 60;
        return $"{sign}{wholeDegrees:00}:{minutes:00}:{seconds:00.###}";
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static double NormalizeRadians(double radians)
    {
        while (radians > Math.PI)
        {
            radians -= Math.PI * 2;
        }

        while (radians < -Math.PI)
        {
            radians += Math.PI * 2;
        }

        return radians;
    }
}
