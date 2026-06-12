using System.Globalization;

namespace WinDS9.Engine;

public sealed class RegionEditService
{
    public Ds9Region Move(Ds9Region region, double dx, double dy)
    {
        var values = region.Values.ToList();
        switch (region.Kind)
        {
            case Ds9RegionKind.Circle:
            case Ds9RegionKind.Box:
            case Ds9RegionKind.Ellipse:
            case Ds9RegionKind.Point:
            case Ds9RegionKind.Annulus:
            case Ds9RegionKind.Text:
                values[0] += dx;
                values[1] += dy;
                break;
            default:
                for (var i = 0; i + 1 < values.Count; i += 2)
                {
                    values[i] += dx;
                    values[i + 1] += dy;
                }

                break;
        }

        return region with { Values = values };
    }

    public bool TryUpdateValues(Ds9Region region, string valuesText, out Ds9Region updated)
    {
        updated = region;
        var values = valuesText
            .Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseValue)
            .ToList();

        if (values.Any(value => !value.HasValue))
        {
            return false;
        }

        var parsed = values.Select(value => value!.Value).ToArray();
        if (!HasRequiredValueCount(region.Kind, parsed.Length))
        {
            return false;
        }

        updated = region with { Values = parsed };
        return true;
    }

    public Ds9Region WithLabel(Ds9Region region, string? label)
    {
        return region with { Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim() };
    }

    private static double? ParseValue(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
               double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)
            ? parsed
            : null;
    }

    private static bool HasRequiredValueCount(Ds9RegionKind kind, int count)
    {
        return kind switch
        {
            Ds9RegionKind.Circle => count >= 3,
            Ds9RegionKind.Box => count >= 4,
            Ds9RegionKind.Ellipse => count >= 4,
            Ds9RegionKind.Point => count >= 2,
            Ds9RegionKind.Line => count >= 4,
            Ds9RegionKind.Polygon => count >= 6 && count % 2 == 0,
            Ds9RegionKind.Annulus => count >= 4,
            Ds9RegionKind.Text => count >= 2,
            Ds9RegionKind.Ruler => count >= 4,
            Ds9RegionKind.Vector => count >= 4,
            Ds9RegionKind.Segment => count >= 4 && count % 2 == 0,
            Ds9RegionKind.Projection => count >= 5,
            _ => false
        };
    }
}
