using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace WinDS9.WinUI.Controllers;

internal sealed class InspectorController
{
    private readonly IReadOnlyList<Section> sections;
    private readonly Border sizer;
    private readonly Func<string, Brush> brushProvider;
    private Button? hoveredHeader;

    public InspectorController(
        IReadOnlyList<Section> sections,
        Border sizer,
        Func<string, Brush> brushProvider)
    {
        this.sections = sections;
        this.sizer = sizer;
        this.brushProvider = brushProvider;

        foreach (var section in sections)
        {
            section.HeaderButton.PointerEntered += HeaderButton_PointerEntered;
            section.HeaderButton.PointerExited += HeaderButton_PointerExited;
        }
    }

    public void Toggle(Button button)
    {
        foreach (var section in sections.Where(section => ReferenceEquals(section.HeaderButton, button)))
        {
            var shouldOpen = section.Content.Visibility != Visibility.Visible;
            section.Content.Visibility = shouldOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        ApplyState();
    }

    public void Expand(FrameworkElement content)
    {
        foreach (var section in sections)
        {
            if (!ReferenceEquals(section.Content, content))
            {
                continue;
            }

            section.Content.Visibility = Visibility.Visible;
        }

        ApplyState();
    }

    public void ApplyState()
    {
        foreach (var section in sections)
        {
            var isActive = section.Content.Visibility == Visibility.Visible;
            var isHovered = ReferenceEquals(section.HeaderButton, hoveredHeader);
            section.Container.Background = isActive
                ? Brush("InspectorSectionActiveBrush")
                : isHovered
                    ? Brush("InspectorSectionHoverBrush")
                    : Brush("InspectorSectionDefaultBrush");
            section.Container.BorderBrush = isActive
                ? Brush("InspectorSectionActiveStrokeBrush")
                : isHovered
                    ? Brush("InspectorSectionHoverStrokeBrush")
                    : Brush("InspectorSectionDefaultStrokeBrush");
            section.Container.BorderThickness = new Thickness(1);
            section.HeaderButton.Background = Brush("InvisibleBrush");
            section.HeaderButton.BorderBrush = Brush("InvisibleBrush");
            section.HeaderButton.BorderThickness = new Thickness(0);
            UpdateChevron(section.HeaderButton, isActive);
        }
    }

    public void UpdateSizerVisual(bool isResizing, bool isPointerOver)
    {
        sizer.Background = isResizing || isPointerOver
            ? Brush("SizerHoverBrush")
            : Brush("InvisibleBrush");
    }

    private void HeaderButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        hoveredHeader = sender as Button;
        ApplyState();
    }

    private void HeaderButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (ReferenceEquals(hoveredHeader, sender))
        {
            hoveredHeader = null;
        }

        ApplyState();
    }

    private Brush Brush(string key) => brushProvider(key);

    private static void UpdateChevron(Button button, bool isExpanded)
    {
        if (button.Content is not DependencyObject content)
        {
            return;
        }

        var icon = FindRightChevron(content);
        if (icon is not null)
        {
            icon.Glyph = isExpanded ? "\uE70E" : "\uE70D";
        }
    }

    private static FontIcon? FindRightChevron(DependencyObject root)
    {
        if (root is FontIcon icon && Grid.GetColumn(icon) == 2)
        {
            return icon;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var childIcon = FindRightChevron(VisualTreeHelper.GetChild(root, index));
            if (childIcon is not null)
            {
                return childIcon;
            }
        }

        return null;
    }

    internal sealed record Section(Border Container, Button HeaderButton, FrameworkElement Content);
}
