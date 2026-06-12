using System.Globalization;

namespace WinDS9.Engine;

public sealed class FitsHeader
{
    private readonly Dictionary<string, string> values;

    public FitsHeader(IReadOnlyList<string> cards)
    {
        Cards = cards;
        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in cards)
        {
            if (card.Length < 10 || card[8] != '=')
            {
                continue;
            }

            var key = card[..8].Trim();
            var raw = card[10..];
            values[key] = ParseRawValue(raw);
        }
    }

    public IReadOnlyList<string> Cards { get; }

    public string? GetString(string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }

    public int GetInt32(string key, int fallback = 0)
    {
        return int.TryParse(GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    public double? GetDouble(string key)
    {
        return double.TryParse(GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public double GetDouble(string key, double fallback)
    {
        return GetDouble(key) ?? fallback;
    }

    private static string ParseRawValue(string raw)
    {
        var trimmed = raw.TrimStart();
        if (trimmed.StartsWith('\''))
        {
            var end = trimmed.IndexOf('\'', 1);
            return end > 0 ? trimmed[1..end].TrimEnd() : trimmed.Trim('\'').Trim();
        }

        var slash = raw.IndexOf('/');
        return (slash >= 0 ? raw[..slash] : raw).Trim();
    }
}
