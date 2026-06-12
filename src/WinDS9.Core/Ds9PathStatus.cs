namespace WinDS9.Core;

public sealed record Ds9PathStatus(
    bool IsUsable,
    string? ExecutablePath,
    string? InstallDirectory,
    string? XpaSetPath,
    string? XpaGetPath,
    string? XpansPath,
    string Message,
    IReadOnlyList<string> CheckedPaths);
