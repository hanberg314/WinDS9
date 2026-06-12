namespace WinDS9.Engine;

public sealed record FitsFileInfo(string Path, IReadOnlyList<FitsHduInfo> Hdus);
