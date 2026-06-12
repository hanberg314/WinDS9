namespace WinDS9.Core;

public sealed class AppSettings
{
    public string? Ds9ExecutablePath { get; set; }
    public string ProfileId { get; set; } = LaunchProfile.DefaultProfileId;
    public bool OpenInNewFrame { get; set; } = true;
    public bool TryReuseRunningDs9 { get; set; } = true;
    public List<string> RecentFiles { get; set; } = [];
}
