namespace WinDS9.Core;

public sealed class Ds9PathDetector
{
    public Ds9PathStatus Detect(string workspaceRoot, string? configuredPath)
    {
        var candidates = BuildCandidates(workspaceRoot, configuredPath);
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate.Path))
            {
                var installDirectory = Path.GetDirectoryName(candidate.Path) ?? workspaceRoot;
                return new Ds9PathStatus(
                    true,
                    candidate.Path,
                    installDirectory,
                    FindSiblingTool(installDirectory, "xpaset.exe"),
                    FindSiblingTool(installDirectory, "xpaget.exe"),
                    FindSiblingTool(installDirectory, "xpans.exe"),
                    $"Using DS9 executable from {candidate.Label}.",
                    candidates.Select(item => item.Path).ToList());
            }
        }

        var sourceTree = Path.Combine(workspaceRoot, "vendor", "SAOImageDS9");
        var sourceMessage = Directory.Exists(sourceTree)
            ? " Found SAOImageDS9 source under vendor, but no built Windows ds9.exe there."
            : string.Empty;

        return new Ds9PathStatus(
            false,
            null,
            null,
            null,
            null,
            null,
            "No usable ds9.exe found. Put the Windows binary folder under vendor\\ds9 or vendor\\win ver, or set a custom ds9.exe path." + sourceMessage,
            candidates.Select(item => item.Path).ToList());
    }

    private static IReadOnlyList<PathCandidate> BuildCandidates(string workspaceRoot, string? configuredPath)
    {
        var candidates = new List<PathCandidate>();
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            candidates.Add(new PathCandidate("configured path", NormalizeExecutablePath(configuredPath)));
        }

        candidates.Add(new PathCandidate("vendor\\ds9", Path.Combine(workspaceRoot, "vendor", "ds9", "ds9.exe")));
        candidates.Add(new PathCandidate("vendor\\win ver", Path.Combine(workspaceRoot, "vendor", "win ver", "ds9.exe")));
        candidates.Add(new PathCandidate("win ver", Path.Combine(workspaceRoot, "win ver", "ds9.exe")));
        candidates.Add(new PathCandidate("vendor\\SAOImageDS9", Path.Combine(workspaceRoot, "vendor", "SAOImageDS9", "ds9.exe")));
        candidates.Add(new PathCandidate("vendor\\SAOImageDS9\\ds9\\win", Path.Combine(workspaceRoot, "vendor", "SAOImageDS9", "ds9", "win", "ds9.exe")));

        return candidates;
    }

    private static string NormalizeExecutablePath(string configuredPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
        if (Directory.Exists(expanded))
        {
            return Path.Combine(expanded, "ds9.exe");
        }

        return expanded;
    }

    private static string? FindSiblingTool(string installDirectory, string fileName)
    {
        var path = Path.Combine(installDirectory, fileName);
        return File.Exists(path) ? path : null;
    }

    private sealed record PathCandidate(string Label, string Path);
}
