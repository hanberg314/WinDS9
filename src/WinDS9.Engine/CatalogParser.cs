using System.Globalization;

namespace WinDS9.Engine;

public sealed class CatalogParser
{
    public IReadOnlyList<CatalogEntry> ParseFile(string path)
    {
        return Parse(File.ReadAllText(path), DetectDelimiter(path));
    }

    public IReadOnlyList<CatalogEntry> Parse(string text, char? delimiter = ',')
    {
        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();
        if (lines.Count < 2)
        {
            return [];
        }

        var actualDelimiter = delimiter ?? DetectDelimiter(lines[0]);
        var headers = Split(lines[0], actualDelimiter).Select(NormalizeHeader).ToList();
        var xIndex = FindColumn(headers, "x", "imagex", "image_x", "x_image", "physicalx", "physical_x");
        var yIndex = FindColumn(headers, "y", "imagey", "image_y", "y_image", "physicaly", "physical_y");
        var coordinateSystem = CatalogCoordinateSystem.Image;
        var skyFrame = "fk5";

        if (xIndex < 0 || yIndex < 0)
        {
            xIndex = FindColumn(headers, "ra", "raj2000", "ra_j2000", "alpha", "rightascension");
            yIndex = FindColumn(headers, "dec", "dej2000", "decj2000", "dec_j2000", "delta", "declination");
            coordinateSystem = CatalogCoordinateSystem.Sky;
            skyFrame = "fk5";
        }

        if (xIndex < 0 || yIndex < 0)
        {
            xIndex = FindColumn(headers, "glon", "l", "galacticlongitude");
            yIndex = FindColumn(headers, "glat", "b", "galacticlatitude");
            coordinateSystem = CatalogCoordinateSystem.Sky;
            skyFrame = "galactic";
        }

        if (xIndex < 0 || yIndex < 0)
        {
            return [];
        }

        var labelIndex = FindColumn(headers, "name", "label", "id", "source", "object");
        var entries = new List<CatalogEntry>();
        foreach (var line in lines.Skip(1))
        {
            var fields = Split(line, actualDelimiter).ToList();
            if (fields.Count <= Math.Max(xIndex, yIndex) ||
                !TryParse(fields[xIndex], out var x) ||
                !TryParse(fields[yIndex], out var y))
            {
                continue;
            }

            var label = labelIndex >= 0 && labelIndex < fields.Count ? fields[labelIndex].Trim().Trim('"') : null;
            var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < Math.Min(headers.Count, fields.Count); i++)
            {
                columns[headers[i]] = fields[i].Trim().Trim('"');
            }

            entries.Add(new CatalogEntry(
                x,
                y,
                string.IsNullOrWhiteSpace(label) ? null : label,
                coordinateSystem,
                skyFrame,
                columns));
        }

        return entries;
    }

    private static char? DetectDelimiter(string pathOrHeader)
    {
        if (Path.GetExtension(pathOrHeader).Equals(".tsv", StringComparison.OrdinalIgnoreCase))
        {
            return '\t';
        }

        if (pathOrHeader.Contains('\t'))
        {
            return '\t';
        }

        if (pathOrHeader.Contains(','))
        {
            return ',';
        }

        return null;
    }

    private static IEnumerable<string> Split(string line, char? delimiter)
    {
        return delimiter is null
            ? line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : SplitDelimited(line, delimiter.Value);
    }

    private static IEnumerable<string> SplitDelimited(string line, char delimiter)
    {
        var fields = new List<string>();
        var start = 0;
        var inQuote = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inQuote = !inQuote;
            }
            else if (!inQuote && line[i] == delimiter)
            {
                fields.Add(line[start..i].Trim());
                start = i + 1;
            }
        }

        fields.Add(line[start..].Trim());
        return fields;
    }

    private static int FindColumn(IReadOnlyList<string> headers, params string[] names)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            if (names.Contains(headers[i], StringComparer.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string NormalizeHeader(string value)
    {
        return value.Trim().Trim('"').Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
    }

    private static bool TryParse(string value, out double result)
    {
        var trimmed = value.Trim().Trim('"');
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        return TryParseSexagesimal(trimmed, out result);
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
}
