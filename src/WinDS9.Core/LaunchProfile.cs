namespace WinDS9.Core;

public sealed record LaunchProfile(
    string Id,
    string DisplayName,
    string? Geometry,
    IReadOnlyList<string> StartupArguments,
    IReadOnlyList<IReadOnlyList<string>> XpaCommands)
{
    public const string DefaultProfileId = "quick-look";

    public static IReadOnlyList<LaunchProfile> Defaults { get; } =
    [
        new LaunchProfile(
            DefaultProfileId,
            "Quick look: zscale + fit",
            "1100x760",
            ["-zscale", "-zoom", "to", "fit"],
            [["zscale"], ["zoom", "to", "fit"]]),

        new LaunchProfile(
            "plain",
            "Plain DS9",
            null,
            [],
            []),

        new LaunchProfile(
            "large-window",
            "Large window: zscale + fit",
            "1400x900",
            ["-zscale", "-zoom", "to", "fit"],
            [["zscale"], ["zoom", "to", "fit"]])
    ];

    public static LaunchProfile Resolve(string? id)
    {
        return Defaults.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? Defaults[0];
    }

    public override string ToString() => DisplayName;
}
