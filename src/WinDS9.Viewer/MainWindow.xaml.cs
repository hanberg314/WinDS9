using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinDS9.Engine;

namespace WinDS9.Viewer;

public partial class MainWindow : Window
{
    private readonly string[] startupFiles;
    private readonly NativeImageLoader loader = new();
    private readonly RasterRenderer renderer = new();
    private LoadedImage? currentImage;
    private double zoom = 1;
    private bool isLoading;

    public MainWindow(string[]? startupFiles = null)
    {
        this.startupFiles = startupFiles ?? [];
        InitializeComponent();
        StretchCombo.ItemsSource = Enum.GetValues<ImageStretch>();
        StretchCombo.SelectedItem = ImageStretch.Log;
        Loaded += MainWindow_Loaded;
        ImageScroll.SizeChanged += (_, _) => FitIfNeeded();
        SetStatus("Ready");
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        foreach (var path in startupFiles.Where(File.Exists))
        {
            await OpenPathAsync(path);
        }
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        var samplePath = Path.Combine(FindWorkspaceRoot(), "samples");
        var dialog = new OpenFileDialog
        {
            Filter = "FITS and event files|*.fits;*.fit;*.fts;*.evt;*.fits.gz;*.fit.gz|All files|*.*",
            Multiselect = false,
            InitialDirectory = Directory.Exists(samplePath) ? samplePath : FindWorkspaceRoot()
        };

        if (dialog.ShowDialog(this) == true)
        {
            await OpenPathAsync(dialog.FileName);
        }
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            var first = files.FirstOrDefault(File.Exists);
            if (first is not null)
            {
                await OpenPathAsync(first);
            }
        }
    }

    private async Task OpenPathAsync(string path)
    {
        if (isLoading)
        {
            return;
        }

        try
        {
            isLoading = true;
            BusyBar.Visibility = Visibility.Visible;
            SetStatus($"Loading {Path.GetFileName(path)}");
            AddLog($"open {path}");

            var loaded = await Task.Run(() => loader.Load(path));
            currentImage = loaded;
            UpdateMetadata(loaded);
            await RenderCurrentAsync(fitAfterRender: true);
            AddLog($"loaded {loaded.Width}x{loaded.Height} in {loaded.LoadDuration.TotalMilliseconds:0} ms");
        }
        catch (Exception ex)
        {
            SetStatus("Open failed");
            AddLog(ex.Message);
            MessageBox.Show(this, ex.Message, "WinDS9 Native", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            BusyBar.Visibility = Visibility.Collapsed;
            isLoading = false;
        }
    }

    private async void StretchCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (currentImage is not null)
        {
            await RenderCurrentAsync(fitAfterRender: false);
        }
    }

    private async Task RenderCurrentAsync(bool fitAfterRender)
    {
        if (currentImage is null)
        {
            return;
        }

        var stretch = StretchCombo.SelectedItem is ImageStretch selected ? selected : ImageStretch.Log;
        var renderWatch = Stopwatch.StartNew();
        var rendered = await Task.Run(() => renderer.Render(currentImage, stretch));
        renderWatch.Stop();

        ImageView.Source = ToBitmap(rendered);
        ImageHost.Width = rendered.Width * zoom;
        ImageHost.Height = rendered.Height * zoom;
        CutText.Text = $"{rendered.LowCut:0.###} - {rendered.HighCut:0.###}";
        SetStatus($"Rendered {rendered.Width}x{rendered.Height} in {renderWatch.Elapsed.TotalMilliseconds:0} ms");

        if (fitAfterRender)
        {
            FitToViewport();
        }
    }

    private static BitmapSource ToBitmap(RenderedRaster raster)
    {
        var bitmap = BitmapSource.Create(
            raster.Width,
            raster.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            raster.Bgra32,
            raster.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private void Fit_Click(object sender, RoutedEventArgs e)
    {
        FitToViewport();
    }

    private void ActualSize_Click(object sender, RoutedEventArgs e)
    {
        SetZoom(1);
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        SetZoom(zoom * 1.25);
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        SetZoom(zoom / 1.25);
    }

    private void Window_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (currentImage is null || !Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            return;
        }

        SetZoom(e.Delta > 0 ? zoom * 1.15 : zoom / 1.15);
        e.Handled = true;
    }

    private void FitIfNeeded()
    {
        if (currentImage is not null && Math.Abs(zoom - 1) < 0.001)
        {
            FitToViewport();
        }
    }

    private void FitToViewport()
    {
        if (currentImage is null || ImageScroll.ViewportWidth <= 0 || ImageScroll.ViewportHeight <= 0)
        {
            return;
        }

        var fitX = ImageScroll.ViewportWidth / currentImage.Width;
        var fitY = ImageScroll.ViewportHeight / currentImage.Height;
        SetZoom(Math.Min(fitX, fitY) * 0.98);
    }

    private void SetZoom(double value)
    {
        zoom = Math.Clamp(value, 0.05, 32);
        ImageScale.ScaleX = zoom;
        ImageScale.ScaleY = zoom;

        if (currentImage is not null)
        {
            ImageHost.Width = currentImage.Width * zoom;
            ImageHost.Height = currentImage.Height * zoom;
        }

        ZoomText.Text = $"{zoom * 100:0.#}%";
    }

    private void UpdateMetadata(LoadedImage image)
    {
        FileText.Text = image.FilePath;
        HduText.Text = image.HduIndex.ToString();
        TypeText.Text = image.SourceKind;
        RasterText.Text = image.SourceWidth > 0 && image.SourceHeight > 0
            ? $"{image.Width} x {image.Height} from {image.SourceWidth} x {image.SourceHeight}"
            : $"{image.Width} x {image.Height}";
        RowsText.Text = image.BinnedRows > 0
            ? $"{image.BinnedRows:N0} / {image.SourceRows:N0}"
            : image.SourceRows.ToString("N0");
        RangeText.Text = $"{image.DataMin:0.###} - {image.DataMax:0.###}";
        CutText.Text = $"{image.LowCut:0.###} - {image.HighCut:0.###}";
        LoadText.Text = $"{image.LoadDuration.TotalMilliseconds:0} ms";
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void AddLog(string message)
    {
        LogList.Items.Insert(0, $"{DateTimeOffset.Now:HH:mm:ss} {message}");
        while (LogList.Items.Count > 200)
        {
            LogList.Items.RemoveAt(LogList.Items.Count - 1);
        }
    }

    private static string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WinDS9.sln")) ||
                Directory.Exists(Path.Combine(current.FullName, "samples")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
