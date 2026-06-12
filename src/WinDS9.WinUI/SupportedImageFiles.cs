namespace WinDS9.WinUI;

internal static class SupportedImageFiles
{
    private static readonly string[] ExtensionValues =
    [
        ".fits",
        ".fit",
        ".fts",
        ".evt"
    ];

    private static readonly HashSet<string> ExtensionSet = new(ExtensionValues, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Extensions => ExtensionValues;

    public static bool IsSupported(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && ExtensionSet.Contains(Path.GetExtension(path));
    }

    public static string? FirstExistingSupported(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (IsSupported(path) && File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        return null;
    }
}
