using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinDS9.Core;

namespace WinDS9.App;

public partial class MainWindow : Window
{
    private readonly string[] startupFiles;
    private readonly string workspaceRoot;
    private readonly SettingsStore settingsStore;
    private readonly Ds9PathDetector pathDetector = new();
    private readonly Ds9Launcher launcher;
    private readonly RecentFilesService recentFilesService = new();
    private AppSettings settings;

    public MainWindow(string[]? startupFiles = null)
    {
        this.startupFiles = startupFiles ?? [];
        workspaceRoot = WorkspaceRootLocator.Find();
        settingsStore = new SettingsStore();
        settings = settingsStore.Load();
        launcher = new Ds9Launcher(pathDetector, new Ds9CommandBuilder());

        InitializeComponent();
        InitializeUi();
        Loaded += MainWindow_Loaded;
    }

    private void InitializeUi()
    {
        ProfileCombo.ItemsSource = LaunchProfile.Defaults;
        ProfileCombo.SelectedItem = LaunchProfile.Resolve(settings.ProfileId);
        NewFrameCheck.IsChecked = settings.OpenInNewFrame;
        ReuseCheck.IsChecked = settings.TryReuseRunningDs9;

        var status = RefreshPathStatus();
        Ds9PathBox.Text = settings.Ds9ExecutablePath ?? status.ExecutablePath ?? Path.Combine(workspaceRoot, "vendor", "win ver", "ds9.exe");

        RefreshRecentFiles();
        AddLog($"Workspace: {workspaceRoot}");
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        foreach (var file in startupFiles.Where(File.Exists))
        {
            await OpenPathAsync(file);
        }
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var sampleDirectory = Path.Combine(workspaceRoot, "samples");
        var dialog = new OpenFileDialog
        {
            Filter = "FITS and event files|*.fits;*.fit;*.fts;*.evt;*.fits.gz;*.fit.gz|All files|*.*",
            Multiselect = true,
            InitialDirectory = Directory.Exists(sampleDirectory) ? sampleDirectory : workspaceRoot
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        foreach (var fileName in dialog.FileNames)
        {
            await OpenPathAsync(fileName);
        }
    }

    private void BrowseDs9_Click(object sender, RoutedEventArgs e)
    {
        var binaryDirectory = Path.Combine(workspaceRoot, "vendor", "win ver");
        var dialog = new OpenFileDialog
        {
            Filter = "DS9 executable|ds9.exe|Executable files|*.exe|All files|*.*",
            InitialDirectory = Directory.Exists(binaryDirectory) ? binaryDirectory : workspaceRoot
        };

        if (dialog.ShowDialog(this) == true)
        {
            Ds9PathBox.Text = dialog.FileName;
            SavePath();
        }
    }

    private void SavePath_Click(object sender, RoutedEventArgs e)
    {
        SavePath();
    }

    private void SavePath()
    {
        settings.Ds9ExecutablePath = string.IsNullOrWhiteSpace(Ds9PathBox.Text) ? null : Ds9PathBox.Text.Trim();
        SaveSettings();
        RefreshPathStatus();
    }

    private async void RecentFilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        await OpenSelectedRecentAsync();
    }

    private async void OpenRecent_Click(object sender, RoutedEventArgs e)
    {
        await OpenSelectedRecentAsync();
    }

    private void ClearRecent_Click(object sender, RoutedEventArgs e)
    {
        settings.RecentFiles.Clear();
        SaveSettings();
        RefreshRecentFiles();
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileCombo.SelectedItem is LaunchProfile profile)
        {
            settings.ProfileId = profile.Id;
            SaveSettings();
        }
    }

    private void SettingsChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        settings.OpenInNewFrame = NewFrameCheck.IsChecked == true;
        settings.TryReuseRunningDs9 = ReuseCheck.IsChecked == true;
        SaveSettings();
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        foreach (var file in files.Where(File.Exists))
        {
            await OpenPathAsync(file);
        }
    }

    private async Task OpenSelectedRecentAsync()
    {
        if (RecentFilesList.SelectedItem is string path)
        {
            await OpenPathAsync(path);
        }
    }

    private async Task OpenPathAsync(string path)
    {
        var profile = ProfileCombo.SelectedItem as LaunchProfile ?? LaunchProfile.Resolve(settings.ProfileId);
        AddLog($"Opening: {path}");

        var result = await launcher.OpenAsync(workspaceRoot, settings, path, profile);
        if (result.Success)
        {
            settings.RecentFiles = recentFilesService.Add(path, settings.RecentFiles).ToList();
            SaveSettings();
            RefreshRecentFiles();
            AddLog($"{result.Duration.TotalMilliseconds:0} ms | {(result.UsedXpa ? "XPA" : "process")} | {result.Message}");
            return;
        }

        AddLog($"Failed | {result.Message}");
        MessageBox.Show(this, result.Message, "WinDS9", MessageBoxButton.OK, MessageBoxImage.Warning);
        RefreshPathStatus();
    }

    private Ds9PathStatus RefreshPathStatus()
    {
        var status = pathDetector.Detect(workspaceRoot, settings.Ds9ExecutablePath);
        var xpa = status.XpaSetPath is null ? "xpaset: missing" : "xpaset: available";
        var xpans = status.XpansPath is null ? "xpans: missing" : "xpans: available";
        PathStatusText.Text = $"{status.Message} {xpa}; {xpans}.";
        return status;
    }

    private void RefreshRecentFiles()
    {
        RecentFilesList.ItemsSource = null;
        RecentFilesList.ItemsSource = settings.RecentFiles;
    }

    private void SaveSettings()
    {
        settingsStore.Save(settings);
    }

    private void AddLog(string message)
    {
        LogList.Items.Insert(0, $"{DateTimeOffset.Now:HH:mm:ss}  {message}");
        while (LogList.Items.Count > 100)
        {
            LogList.Items.RemoveAt(LogList.Items.Count - 1);
        }
    }
}
