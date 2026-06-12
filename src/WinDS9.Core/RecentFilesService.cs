namespace WinDS9.Core;

public sealed class RecentFilesService
{
    public const int MaxRecentFiles = 12;

    public IReadOnlyList<string> Add(string filePath, IEnumerable<string> current)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return current.ToList();
        }

        var fullPath = Path.GetFullPath(filePath);
        var result = new List<string> { fullPath };

        foreach (var item in current)
        {
            if (result.Count >= MaxRecentFiles)
            {
                break;
            }

            if (!string.Equals(fullPath, item, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(item);
            }
        }

        return result;
    }
}
