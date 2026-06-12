using WinDS9.Engine;

namespace WinDS9.WinUI.Controllers;

internal sealed class CommandExecutionController
{
    private readonly CommandDispatcher commandDispatcher;
    private readonly Func<string, Task> openPathAsync;
    private readonly Action<IReadOnlyList<string>> applyScale;
    private readonly Action<IReadOnlyList<string>> applyColorMap;
    private readonly Action<IReadOnlyList<string>> applyZoom;
    private readonly Func<IReadOnlyList<string>, Task> applyFrameAsync;
    private readonly Action<IReadOnlyList<string>> applyRegion;
    private readonly Action<string> loadCatalog;
    private readonly Action executeContour;
    private readonly Action<string> setStatus;
    private readonly Action<string> addLog;

    public CommandExecutionController(
        CommandDispatcher commandDispatcher,
        Func<string, Task> openPathAsync,
        Action<IReadOnlyList<string>> applyScale,
        Action<IReadOnlyList<string>> applyColorMap,
        Action<IReadOnlyList<string>> applyZoom,
        Func<IReadOnlyList<string>, Task> applyFrameAsync,
        Action<IReadOnlyList<string>> applyRegion,
        Action<string> loadCatalog,
        Action executeContour,
        Action<string> setStatus,
        Action<string> addLog)
    {
        this.commandDispatcher = commandDispatcher;
        this.openPathAsync = openPathAsync;
        this.applyScale = applyScale;
        this.applyColorMap = applyColorMap;
        this.applyZoom = applyZoom;
        this.applyFrameAsync = applyFrameAsync;
        this.applyRegion = applyRegion;
        this.loadCatalog = loadCatalog;
        this.executeContour = executeContour;
        this.setStatus = setStatus;
        this.addLog = addLog;
    }

    public async Task ExecuteAsync(string commandText)
    {
        var command = commandDispatcher.Parse(commandText);
        if (command.Kind == Ds9CommandKind.Unknown)
        {
            setStatus("Unknown command");
            addLog($"unknown command {commandText}");
            return;
        }

        try
        {
            switch (command.Kind)
            {
                case Ds9CommandKind.Open:
                    if (command.Arguments.Count == 0)
                    {
                        setStatus("open requires a path");
                        return;
                    }

                    await openPathAsync(command.Arguments[0]);
                    break;
                case Ds9CommandKind.Scale:
                    applyScale(command.Arguments);
                    break;
                case Ds9CommandKind.ColorMap:
                    applyColorMap(command.Arguments);
                    break;
                case Ds9CommandKind.Zoom:
                    applyZoom(command.Arguments);
                    break;
                case Ds9CommandKind.Frame:
                    await applyFrameAsync(command.Arguments);
                    break;
                case Ds9CommandKind.Region:
                    applyRegion(command.Arguments);
                    break;
                case Ds9CommandKind.Catalog:
                    if (command.Arguments.Count > 0)
                    {
                        loadCatalog(command.Arguments[0]);
                    }
                    break;
                case Ds9CommandKind.Contour:
                    executeContour();
                    break;
                case Ds9CommandKind.Pan:
                    setStatus("pan command is parsed; viewport pan execution is not wired yet");
                    break;
            }

            addLog($"cmd {commandText}");
        }
        catch (Exception ex)
        {
            addLog(ex.Message);
            setStatus("Command failed");
        }
    }
}
