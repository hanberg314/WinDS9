using System.Diagnostics;

namespace WinDS9.Core;

public sealed class Ds9Launcher
{
    private const string XpaTarget = "ds9";

    private readonly Ds9PathDetector pathDetector;
    private readonly Ds9CommandBuilder commandBuilder;
    private readonly IProcessRunner processRunner;

    public Ds9Launcher(
        Ds9PathDetector pathDetector,
        Ds9CommandBuilder commandBuilder,
        IProcessRunner? processRunner = null)
    {
        this.pathDetector = pathDetector;
        this.commandBuilder = commandBuilder;
        this.processRunner = processRunner ?? new ProcessRunner();
    }

    public async Task<Ds9LaunchResult> OpenAsync(
        string workspaceRoot,
        AppSettings settings,
        string? filePath,
        LaunchProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(profile);

        var startedAt = Stopwatch.GetTimestamp();
        var status = pathDetector.Detect(workspaceRoot, settings.Ds9ExecutablePath);
        if (!status.IsUsable || status.ExecutablePath is null)
        {
            return new Ds9LaunchResult(
                false,
                false,
                Stopwatch.GetElapsedTime(startedAt),
                status.Message,
                null,
                []);
        }

        if (!string.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath))
        {
            return new Ds9LaunchResult(
                false,
                false,
                Stopwatch.GetElapsedTime(startedAt),
                $"File does not exist: {filePath}",
                status.ExecutablePath,
                []);
        }

        if (settings.TryReuseRunningDs9 &&
            !string.IsNullOrWhiteSpace(filePath) &&
            status.XpaSetPath is not null &&
            IsDs9ProcessRunning())
        {
            var xpaArgs = commandBuilder.BuildXpaOpenArguments(XpaTarget, filePath, settings.OpenInNewFrame);
            var xpaResult = await processRunner.RunAsync(
                status.XpaSetPath,
                xpaArgs,
                status.InstallDirectory,
                TimeSpan.FromSeconds(3),
                cancellationToken);

            if (xpaResult.ExitCode == 0)
            {
                foreach (var profileCommand in commandBuilder.BuildXpaProfileCommands(XpaTarget, profile))
                {
                    await processRunner.RunAsync(
                        status.XpaSetPath,
                        profileCommand,
                        status.InstallDirectory,
                        TimeSpan.FromSeconds(3),
                        cancellationToken);
                }

                return new Ds9LaunchResult(
                    true,
                    true,
                    Stopwatch.GetElapsedTime(startedAt),
                    "Sent file to running DS9 via XPA.",
                    status.XpaSetPath,
                    xpaArgs);
            }
        }

        var startupArgs = commandBuilder.BuildStartupArguments(filePath, profile);
        await processRunner.StartDetachedAsync(status.ExecutablePath, startupArgs, status.InstallDirectory, cancellationToken);

        return new Ds9LaunchResult(
            true,
            false,
            Stopwatch.GetElapsedTime(startedAt),
            "Started DS9.",
            status.ExecutablePath,
            startupArgs);
    }

    private static bool IsDs9ProcessRunning()
    {
        try
        {
            return Process.GetProcessesByName("ds9").Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
