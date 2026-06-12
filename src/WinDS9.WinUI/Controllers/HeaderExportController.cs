using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinDS9.Engine;
using WinDS9.WinUI.Pages;
using WinRT.Interop;

namespace WinDS9.WinUI.Controllers;

internal sealed class HeaderExportController
{
    private readonly Func<ImageFrameViewModel?> getCurrentFrame;
    private readonly Func<RenderedRaster?> getLastRenderedRaster;
    private readonly Func<IntPtr> getWindowHandle;
    private readonly Func<XamlRoot> getXamlRoot;
    private readonly Action<string> setStatus;
    private readonly Action<string> addLog;

    public HeaderExportController(
        Func<ImageFrameViewModel?> getCurrentFrame,
        Func<RenderedRaster?> getLastRenderedRaster,
        Func<IntPtr> getWindowHandle,
        Func<XamlRoot> getXamlRoot,
        Action<string> setStatus,
        Action<string> addLog)
    {
        this.getCurrentFrame = getCurrentFrame;
        this.getLastRenderedRaster = getLastRenderedRaster;
        this.getWindowHandle = getWindowHandle;
        this.getXamlRoot = getXamlRoot;
        this.setStatus = setStatus;
        this.addLog = addLog;
    }

    public async Task PickAndSavePngAsync()
    {
        var frame = getCurrentFrame();
        var raster = getLastRenderedRaster();
        if (frame is null || raster is null)
        {
            setStatus("Render a frame before saving PNG");
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = $"{Path.GetFileNameWithoutExtension(frame.Image.FilePath)}-hdu{frame.Image.HduIndex}.png"
        };
        picker.FileTypeChoices.Add("PNG image", [".png"]);

        InitializeWithWindow.Initialize(picker, getWindowHandle());

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        await SaveRenderedPngAsync(file, raster);
        addLog($"saved png {file.Path}");
        setStatus("PNG saved");
    }

    public async Task ShowHeaderAsync()
    {
        var frame = getCurrentFrame();
        if (frame is null)
        {
            setStatus("No frame");
            return;
        }

        var text = frame.Image.HeaderCards is { Count: > 0 } cards
            ? string.Join(Environment.NewLine, cards)
            : "No FITS header cards are available for this frame.";

        var searchBox = new TextBox
        {
            PlaceholderText = "Search header",
            Margin = new Thickness(0, 0, 0, 8)
        };
        var textBox = new TextBox
        {
            Text = text,
            AcceptsReturn = true,
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.NoWrap,
            MinWidth = 720
        };
        searchBox.TextChanged += (_, _) =>
        {
            var query = searchBox.Text;
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return;
            }

            textBox.Focus(FocusState.Programmatic);
            textBox.SelectionStart = index;
            textBox.SelectionLength = query.Length;
        };
        var scroller = new ScrollViewer
        {
            Content = textBox,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinWidth = 720,
            MinHeight = 420
        };
        var content = new StackPanel
        {
            Children =
            {
                searchBox,
                scroller
            }
        };

        var dialog = new ContentDialog
        {
            XamlRoot = getXamlRoot(),
            Title = $"Header HDU {frame.Image.HduIndex}",
            Content = content,
            SecondaryButtonText = "Copy",
            CloseButtonText = "Close"
        };
        dialog.SecondaryButtonClick += (_, _) =>
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            setStatus("Header copied");
        };
        await dialog.ShowAsync();
    }

    private static async Task SaveRenderedPngAsync(StorageFile file, RenderedRaster raster)
    {
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        stream.Size = 0;
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight,
            (uint)raster.Width,
            (uint)raster.Height,
            96,
            96,
            raster.Bgra32);
        await encoder.FlushAsync();
    }
}
