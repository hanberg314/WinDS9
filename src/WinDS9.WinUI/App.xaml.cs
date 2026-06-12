using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinDS9.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public static Window MainWindow { get; private set; } = null!;
    internal static string StartupLogPath => Path.Combine(AppContext.BaseDirectory, "winui-startup.log");
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        Log("App ctor");
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log($"AppDomain unhandled: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) => Log($"Task unobserved: {e.Exception}");
        InitializeComponent();
        Log("App initialized");
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var launchPath = FindLaunchPath(args.Arguments);
        Log($"OnLaunched path={launchPath ?? "<none>"}");
        _window = new MainWindow(launchPath);
        MainWindow = _window;
        _window.Activate();
        Log("MainWindow activated");
    }

    private static string? FindLaunchPath(string? launchArguments)
    {
        var path = SupportedImageFiles.FirstExistingSupported(Environment.GetCommandLineArgs().Skip(1));
        if (path is not null)
        {
            return path;
        }

        var trimmed = launchArguments?.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(trimmed)
            ? null
            : SupportedImageFiles.FirstExistingSupported([trimmed]);
    }

    internal static void Log(string message)
    {
        try
        {
            File.AppendAllText(StartupLogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Startup logging must never affect app launch.
        }
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log($"XAML unhandled: {e.Exception}");
    }
}
