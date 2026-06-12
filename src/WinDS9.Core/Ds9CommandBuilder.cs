namespace WinDS9.Core;

public sealed class Ds9CommandBuilder
{
    public IReadOnlyList<string> BuildStartupArguments(string? filePath, LaunchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var args = new List<string>();

        if (!string.IsNullOrWhiteSpace(profile.Geometry))
        {
            args.Add("-geometry");
            args.Add(profile.Geometry);
        }

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            args.Add(filePath);
        }

        args.AddRange(profile.StartupArguments);
        return args;
    }

    public IReadOnlyList<string> BuildXpaOpenArguments(string target, string filePath, bool newFrame)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("XPA target is required.", nameof(target));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        var args = new List<string> { "-p", target, "fits" };
        if (newFrame)
        {
            args.Add("new");
        }

        args.Add(filePath);
        return args;
    }

    public IReadOnlyList<IReadOnlyList<string>> BuildXpaProfileCommands(string target, LaunchProfile profile)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("XPA target is required.", nameof(target));
        }

        ArgumentNullException.ThrowIfNull(profile);

        return profile.XpaCommands
            .Select(command =>
            {
                var args = new List<string> { "-p", target };
                args.AddRange(command);
                return (IReadOnlyList<string>)args;
            })
            .ToList();
    }
}
