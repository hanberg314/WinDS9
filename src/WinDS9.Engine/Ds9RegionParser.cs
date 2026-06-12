using System.Globalization;
using System.Text.RegularExpressions;

namespace WinDS9.Engine;

public sealed partial class Ds9RegionParser
{
    public IReadOnlyList<Ds9Region> ParseFile(string path)
    {
        return Parse(File.ReadAllText(path));
    }

    public IReadOnlyList<Ds9Region> Parse(string text)
    {
        var regions = new List<Ds9Region>();
        var coordinateSystem = "image";

        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("global", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsCoordinateSystem(line))
            {
                coordinateSystem = line.ToLowerInvariant();
                continue;
            }

            var parsed = TryParseRegion(line, coordinateSystem);
            if (parsed is not null)
            {
                regions.Add(parsed);
            }
        }

        return regions;
    }

    private static Ds9Region? TryParseRegion(string line, string coordinateSystem)
    {
        var commentStart = line.IndexOf('#');
        var comment = commentStart >= 0 ? line[(commentStart + 1)..] : string.Empty;
        var body = commentStart >= 0 ? line[..commentStart].Trim() : line;
        var match = RegionLineRegex().Match(body);
        if (!match.Success)
        {
            return null;
        }

        var name = match.Groups["name"].Value.ToLowerInvariant();
        var values = SplitArguments(match.Groups["args"].Value)
            .Select(ParseRegionNumber)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        var kind = name switch
        {
            "circle" => Ds9RegionKind.Circle,
            "box" => Ds9RegionKind.Box,
            "ellipse" => Ds9RegionKind.Ellipse,
            "point" => Ds9RegionKind.Point,
            "line" => Ds9RegionKind.Line,
            "polygon" => Ds9RegionKind.Polygon,
            "annulus" => Ds9RegionKind.Annulus,
            "text" => Ds9RegionKind.Text,
            "ruler" => Ds9RegionKind.Ruler,
            "vector" => Ds9RegionKind.Vector,
            "segment" => Ds9RegionKind.Segment,
            "projection" => Ds9RegionKind.Projection,
            _ => (Ds9RegionKind?)null
        };

        if (kind is null || !HasRequiredValueCount(kind.Value, values.Count))
        {
            return null;
        }

        return new Ds9Region(kind.Value, values, coordinateSystem, ExtractLabel(comment));
    }

    private static bool IsCoordinateSystem(string line)
    {
        return string.Equals(line, "image", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(line, "physical", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(line, "fk5", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(line, "icrs", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(line, "galactic", StringComparison.OrdinalIgnoreCase);
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

    private static IEnumerable<string> SplitArguments(string args)
    {
        var parts = new List<string>();
        var start = 0;
        var braceDepth = 0;
        var inQuote = false;
        for (var i = 0; i < args.Length; i++)
        {
            var ch = args[i];
            if (ch is '"' or '\'')
            {
                inQuote = !inQuote;
            }
            else if (!inQuote && ch == '{')
            {
                braceDepth++;
            }
            else if (!inQuote && ch == '}' && braceDepth > 0)
            {
                braceDepth--;
            }
            else if (!inQuote && braceDepth == 0 && ch == ',')
            {
                AddArgument(args[start..i]);
                start = i + 1;
            }
        }

        AddArgument(args[start..]);
        return parts;

        void AddArgument(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.Length > 0)
            {
                parts.Add(trimmed);
            }
        }
    }

    private static double? ParseRegionNumber(string value)
    {
        var trimmed = value.Trim().TrimEnd('"', '\'');
        while (trimmed.Length > 0 && !char.IsDigit(trimmed[^1]) && trimmed[^1] != '.' && trimmed[^1] != '-')
        {
            trimmed = trimmed[..^1];
        }

        if (TryParseSexagesimal(trimmed, out var sexagesimal))
        {
            return sexagesimal;
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static bool TryParseSexagesimal(string value, out double result)
    {
        result = 0;
        if (!value.Contains(':'))
        {
            return false;
        }

        var sign = value.TrimStart().StartsWith('-') ? -1 : 1;
        var parts = value.TrimStart('+', '-').Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts.Length > 3)
        {
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes))
        {
            return false;
        }

        var seconds = 0.0;
        if (parts.Length == 3 &&
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
        {
            return false;
        }

        result = sign * (Math.Abs(degrees) + minutes / 60 + seconds / 3600);
        return true;
    }

    private static string? ExtractLabel(string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        var match = LabelRegex().Match(comment);
        return match.Success ? match.Groups["label"].Value.Trim() : null;
    }

    [GeneratedRegex(@"^(?<name>[A-Za-z]+)\s*\((?<args>[^)]*)\)", RegexOptions.Compiled)]
    private static partial Regex RegionLineRegex();

    [GeneratedRegex(@"\b(?:text|tag)\s*=\s*\{(?<label>[^}]*)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LabelRegex();
}
