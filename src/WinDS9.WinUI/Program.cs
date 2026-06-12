using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace WinDS9.WinUI;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        InitializeWindowsAppRuntime();

        ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }

    private static void InitializeWindowsAppRuntime()
    {
        _ = WindowsAppRuntime_EnsureIsLoaded();

        var runtimeDirectory = TryGetLoadedModuleDirectory("Microsoft.WindowsAppRuntime.dll");
        Environment.SetEnvironmentVariable(
            "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",
            runtimeDirectory ?? AppContext.BaseDirectory);
    }

    private static string? TryGetLoadedModuleDirectory(string moduleName)
    {
        if (!GetModuleHandleEx(0, moduleName, out var moduleHandle))
        {
            return null;
        }

        var path = new StringBuilder(32768);
        var length = GetModuleFileName(moduleHandle, path, path.Capacity);
        if (length == 0)
        {
            return null;
        }

        var directory = Path.GetDirectoryName(path.ToString(0, (int)length));
        return directory is null
            ? null
            : Path.EndsInDirectorySeparator(directory) ? directory : directory + Path.DirectorySeparatorChar;
    }

    [DllImport("Microsoft.WindowsAppRuntime.dll", ExactSpelling = true)]
    private static extern int WindowsAppRuntime_EnsureIsLoaded();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetModuleHandleEx(uint flags, string moduleName, out IntPtr moduleHandle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetModuleFileName(IntPtr moduleHandle, StringBuilder fileName, int size);
}
