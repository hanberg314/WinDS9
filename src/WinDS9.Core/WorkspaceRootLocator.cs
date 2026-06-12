namespace WinDS9.Core;

public static class WorkspaceRootLocator
{
    public static string Find(string? startDirectory = null)
    {
        var current = new DirectoryInfo(startDirectory ?? AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WinDS9.sln")) ||
                Directory.Exists(Path.Combine(current.FullName, "vendor")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
