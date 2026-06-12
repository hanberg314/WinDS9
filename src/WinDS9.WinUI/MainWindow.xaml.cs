using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinDS9.WinUI.Pages;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinDS9.WinUI;

public sealed partial class MainWindow : Window
{
    private const double MinNavPaneLength = 120;
    private const double MaxNavPaneLength = 720;
    private const double MinNavigationContentWidth = 420;

    private readonly HomePage homePage;
    private readonly AboutPage aboutPage = new();
    private readonly SettingsPage settingsPage = new();
    private readonly Dictionary<ImageFrameViewModel, NavigationViewItem> frameNavigationItems = [];
    private bool syncingFrameSelection;
    private bool isResizingNavPane;
    private bool isNavPaneSizerPointerOver;
    private double navResizeStartX;
    private double navResizeStartLength;

    public MainWindow(string? initialPath = null)
    {
        App.Log("MainWindow ctor");
        InitializeComponent();
        App.Log("MainWindow XAML initialized");

        Activated += (_, args) => App.Log($"MainWindow activated event: {args.WindowActivationState}");
        Closed += (_, _) => App.Log("MainWindow closed event");
        AppWindow.Closing += (_, _) => App.Log("AppWindow closing event");

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        SystemBackdrop = new DesktopAcrylicBackdrop();
        NavView.Background = ThemeBrush("NavAcrylicPaneBrush");
        UpdateNavPaneSizer();
        homePage = new HomePage();
        homePage.FramesChanged += (_, _) => RebuildFrameNavigationItems();
        homePage.CurrentFrameChanged += (_, _) => SyncSelectedFrame();
        NavFrame.Content = homePage;
        RebuildFrameNavigationItems();
        App.Log("MainWindow navigated HomePage");

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            homePage.Loaded += OpenInitialPath;

            async void OpenInitialPath(object sender, RoutedEventArgs args)
            {
                homePage.Loaded -= OpenInitialPath;
                await homePage.OpenPathAsync(initialPath);
            }
        }
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
        UpdateNavPaneSizer();
    }

    private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (syncingFrameSelection)
        {
            return;
        }

        if (args.IsSettingsSelected)
        {
            NavFrame.Content = settingsPage;
        }
        else if (args.SelectedItem is NavigationViewItem item)
        {
            if (item.Tag is ImageFrameViewModel frame)
            {
                ShowHomePage(clearSelection: false);
                await homePage.SelectFrameAsync(frame);
                return;
            }

            switch (item.Tag)
            {
                case "edit":
                    ShowHomePage("Edit workspace");
                    break;
                case "frame":
                    ShowHomePage("Frame workspace");
                    break;
                case "analysis":
                    ShowHomePage("Analysis workspace");
                    break;
                case "analysis-wcs":
                    ShowHomePage(clearSelection: true);
                    homePage.ShowWcsSummary();
                    break;
                case "analysis-grid":
                    ShowHomePage(clearSelection: true);
                    homePage.ToggleWcsGrid();
                    break;
                case "analysis-header":
                    ShowHomePage(clearSelection: true);
                    await homePage.ShowHeaderAsync();
                    break;
                case "analysis-contour":
                    ShowHomePage(clearSelection: true);
                    homePage.GenerateContourOverlay();
                    break;
                case "analysis-run":
                    ShowHomePage(clearSelection: true);
                    await homePage.RunAnalysisAsync();
                    break;
                case "about":
                    NavFrame.Content = aboutPage;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown navigation item tag: {item.Tag}");
            }
        }
    }

    private void ShowHomePage(string? status = null, bool clearSelection = false)
    {
        if (!ReferenceEquals(NavFrame.Content, homePage))
        {
            NavFrame.Content = homePage;
        }

        if (clearSelection && NavView.SelectedItem is not null)
        {
            NavView.SelectedItem = null;
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            homePage.SetToolStatus(status);
        }
    }

    private void RebuildFrameNavigationItems()
    {
        foreach (var item in frameNavigationItems.Values)
        {
            NavView.MenuItems.Remove(item);
        }

        frameNavigationItems.Clear();

        var insertIndex = NavView.MenuItems.IndexOf(NavToolsHeader);
        foreach (var frame in homePage.FrameItems)
        {
            var item = CreateFrameNavigationItem(frame);
            frameNavigationItems[frame] = item;
            NavView.MenuItems.Insert(insertIndex, item);
            insertIndex++;
        }

        SyncSelectedFrame();
    }

    private static NavigationViewItem CreateFrameNavigationItem(ImageFrameViewModel frame)
    {
        var item = new NavigationViewItem
        {
            Content = frame.FileName,
            Tag = frame,
            Icon = new FontIcon
            {
                Glyph = frame.CompactDisplayName,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14
            }
        };

        AutomationProperties.SetAutomationId(item, $"NavFrameItem{frame.Index}");
        AutomationProperties.SetName(item, $"Frame {frame.DisplayName}");
        ToolTipService.SetToolTip(item, frame.DisplayName);
        return item;
    }

    private void SyncSelectedFrame()
    {
        syncingFrameSelection = true;
        if (homePage.CurrentFrame is not null &&
            frameNavigationItems.TryGetValue(homePage.CurrentFrame, out var item))
        {
            NavView.SelectedItem = item;
        }
        else if (NavView.SelectedItem is NavigationViewItem selectedItem &&
            selectedItem.Tag is ImageFrameViewModel)
        {
            NavView.SelectedItem = null;
        }

        syncingFrameSelection = false;
    }

    private void NavPaneSizer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        isNavPaneSizerPointerOver = true;
        UpdateNavPaneSizerVisual();
    }

    private void NavPaneSizer_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        isNavPaneSizerPointerOver = false;
        UpdateNavPaneSizerVisual();
    }

    private void NavPaneSizer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!NavView.IsPaneOpen)
        {
            return;
        }

        isResizingNavPane = true;
        navResizeStartX = e.GetCurrentPoint(NavHost).Position.X;
        navResizeStartLength = NavView.OpenPaneLength;
        NavPaneSizer.CapturePointer(e.Pointer);
        UpdateNavPaneSizerVisual();
        e.Handled = true;
    }

    private void NavPaneSizer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!isResizingNavPane)
        {
            return;
        }

        var currentX = e.GetCurrentPoint(NavHost).Position.X;
        NavView.OpenPaneLength = ClampNavPaneLength(navResizeStartLength + currentX - navResizeStartX);
        UpdateNavPaneSizer();
        e.Handled = true;
    }

    private void NavPaneSizer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!isResizingNavPane)
        {
            return;
        }

        isResizingNavPane = false;
        NavPaneSizer.ReleasePointerCaptures();
        UpdateNavPaneSizerVisual();
        e.Handled = true;
    }

    private void UpdateNavPaneSizer()
    {
        if (!NavView.IsPaneOpen)
        {
            NavPaneSizer.Visibility = Visibility.Collapsed;
            return;
        }

        NavPaneSizer.Visibility = Visibility.Visible;
        NavPaneSizer.Margin = new Thickness(Math.Max(0, NavView.OpenPaneLength - NavPaneSizer.Width / 2), 0, 0, 0);
        UpdateNavPaneSizerVisual();
    }

    private double ClampNavPaneLength(double requestedLength)
    {
        var maxByWindow = Math.Max(MinNavPaneLength, NavHost.ActualWidth - MinNavigationContentWidth);
        var maxLength = Math.Min(MaxNavPaneLength, maxByWindow);
        return Math.Clamp(requestedLength, MinNavPaneLength, maxLength);
    }

    private void UpdateNavPaneSizerVisual()
    {
        var alpha = NavView.IsPaneOpen && (isResizingNavPane || isNavPaneSizerPointerOver) ? (byte)42 : (byte)0;
        NavPaneSizer.Background = alpha > 0
            ? ThemeBrush("NavSizerHoverBrush")
            : ThemeBrush("NavSizerTransparentBrush");
    }

    private Brush ThemeBrush(string key) => (Brush)RootShell.Resources[key];
}
