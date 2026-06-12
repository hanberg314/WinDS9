namespace WinDS9.Core;

public sealed record Ds9LaunchResult(
    bool Success,
    bool UsedXpa,
    TimeSpan Duration,
    string Message,
    string? ExecutablePath,
    IReadOnlyList<string> Arguments);
