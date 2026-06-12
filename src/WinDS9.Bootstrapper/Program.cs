using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WinDS9.Bootstrapper;

internal static class Program
{
    private const string PayloadResourceName = "WinDS9Payload.zip";
    private const string PayloadVersion = "v0.1.0-preview.2";
    private const string InnerExecutableName = "WinDS9.WinUI.exe";

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var installDirectory = EnsurePayloadExtracted();
            var executablePath = Path.Combine(installDirectory, InnerExecutableName);
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException("WinDS9 executable was not found after extraction.", executablePath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = installDirectory,
                UseShellExecute = false
            };

            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            Process.Start(startInfo);
            return 0;
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
            return 1;
        }
    }

    private static string EnsurePayloadExtracted()
    {
        var installDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinDS9",
            PayloadVersion);
        var markerPath = Path.Combine(installDirectory, ".payload-version");

        if (File.Exists(Path.Combine(installDirectory, InnerExecutableName)) &&
            File.Exists(markerPath) &&
            string.Equals(File.ReadAllText(markerPath).Trim(), PayloadVersion, StringComparison.Ordinal))
        {
            return installDirectory;
        }

        var tempDirectory = installDirectory + ".tmp-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName)
                ?? throw new InvalidOperationException("WinDS9 payload resource is missing.");
            using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
            archive.ExtractToDirectory(tempDirectory, overwriteFiles: true);
            File.WriteAllText(Path.Combine(tempDirectory, ".payload-version"), PayloadVersion);

            if (Directory.Exists(installDirectory))
            {
                Directory.Delete(installDirectory, recursive: true);
            }

            Directory.Move(tempDirectory, installDirectory);
            return installDirectory;
        }
        catch
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }

            throw;
        }
    }

    private static void ReportFailure(Exception exception)
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinDS9",
            "launcher-error.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.AppendAllText(logPath, $"{DateTimeOffset.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}");
        MessageBox(IntPtr.Zero, $"WinDS9 failed to start. Details were written to:{Environment.NewLine}{logPath}", "WinDS9", 0x00000010);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
