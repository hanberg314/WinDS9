namespace WinDS9.Engine;

public sealed class CommandDispatcher
{
    public Ds9Command Parse(string commandText)
    {
        var tokens = Tokenize(commandText).ToList();
        if (tokens.Count == 0)
        {
            return new Ds9Command(Ds9CommandKind.Unknown, string.Empty, []);
        }

        var name = tokens[0].ToLowerInvariant();
        var kind = name switch
        {
            "open" or "file" or "fits" => Ds9CommandKind.Open,
            "frame" => Ds9CommandKind.Frame,
            "scale" => Ds9CommandKind.Scale,
            "cmap" or "color" or "colormap" => Ds9CommandKind.ColorMap,
            "zoom" => Ds9CommandKind.Zoom,
            "pan" => Ds9CommandKind.Pan,
            "region" or "regions" => Ds9CommandKind.Region,
            "catalog" => Ds9CommandKind.Catalog,
            "contour" => Ds9CommandKind.Contour,
            _ => Ds9CommandKind.Unknown
        };

        return new Ds9Command(kind, name, tokens.Skip(1).ToArray());
    }

    private static IEnumerable<string> Tokenize(string commandText)
    {
        var current = new List<char>();
        var inQuote = false;
        var quote = '\0';
        foreach (var ch in commandText)
        {
            if (inQuote)
            {
                if (ch == quote)
                {
                    inQuote = false;
                }
                else
                {
                    current.Add(ch);
                }

                continue;
            }

            if (ch is '"' or '\'')
            {
                inQuote = true;
                quote = ch;
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (current.Count > 0)
                {
                    yield return new string(current.ToArray());
                    current.Clear();
                }

                continue;
            }

            current.Add(ch);
        }

        if (current.Count > 0)
        {
            yield return new string(current.ToArray());
        }
    }
}
