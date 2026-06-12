using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using WinDS9.Engine;
using WinDS9.WinUI;

namespace WinDS9.WinUI.Controllers;

internal enum RegionEditTool
{
    None,
    Select,
    Point,
    Circle,
    Box,
    Line,
    Text
}

internal sealed class RegionInteractionController
{
    private readonly ComboBox regionToolCombo;
    private readonly TextBlock regionStatusText;
    private readonly TextBlock selectedRegionText;
    private readonly TextBox regionLabelText;
    private readonly TextBox regionValuesText;
    private readonly RegionEditService regionEditService;
    private readonly Func<ImageFrameViewModel?> getCurrentFrame;
    private readonly Action redrawRegions;
    private readonly Action<string> setStatus;
    private readonly Action<string> addLog;

    public RegionInteractionController(
        ComboBox regionToolCombo,
        TextBlock regionStatusText,
        TextBlock selectedRegionText,
        TextBox regionLabelText,
        TextBox regionValuesText,
        RegionEditService regionEditService,
        Func<ImageFrameViewModel?> getCurrentFrame,
        Action redrawRegions,
        Action<string> setStatus,
        Action<string> addLog)
    {
        this.regionToolCombo = regionToolCombo;
        this.regionStatusText = regionStatusText;
        this.selectedRegionText = selectedRegionText;
        this.regionLabelText = regionLabelText;
        this.regionValuesText = regionValuesText;
        this.regionEditService = regionEditService;
        this.getCurrentFrame = getCurrentFrame;
        this.redrawRegions = redrawRegions;
        this.setStatus = setStatus;
        this.addLog = addLog;
    }

    public RegionEditTool CurrentTool { get; private set; } = RegionEditTool.None;

    public int SelectedIndex { get; private set; } = -1;

    public (double X, double Y)? PendingLineStart { get; set; }

    public void Initialize()
    {
        regionToolCombo.ItemsSource = Enum.GetNames<RegionEditTool>();
        regionToolCombo.SelectedItem = RegionEditTool.None.ToString();
        UpdateSelectedRegionControls();
    }

    public void SelectToolFromCombo()
    {
        CurrentTool = Enum.TryParse<RegionEditTool>(regionToolCombo.SelectedItem?.ToString(), out var selected)
            ? selected
            : RegionEditTool.None;
        PendingLineStart = null;
        regionStatusText.Text = CurrentTool switch
        {
            RegionEditTool.Select => "Click a region to select; drag to move",
            RegionEditTool.Point => "Click to add a point",
            RegionEditTool.Circle => "Click to add a circle",
            RegionEditTool.Box => "Click to add a box",
            RegionEditTool.Line => "Click start and end points",
            RegionEditTool.Text => "Click to add text",
            _ => "None"
        };
        UpdateSelectedRegionControls();
    }

    public void ClearSelection(string? regionStatus = null)
    {
        SelectedIndex = -1;
        PendingLineStart = null;
        if (regionStatus is not null)
        {
            regionStatusText.Text = regionStatus;
        }

        UpdateSelectedRegionControls();
    }

    public void SelectRegion(int index, string? regionStatus = null)
    {
        SelectedIndex = index;
        if (regionStatus is not null)
        {
            regionStatusText.Text = regionStatus;
        }

        UpdateSelectedRegionControls();
    }

    public void SelectFirstRegion(string? regionStatus = null)
    {
        SelectedIndex = getCurrentFrame() is { Regions.Count: > 0 } frame ? 0 : -1;
        if (regionStatus is not null)
        {
            regionStatusText.Text = regionStatus;
        }

        UpdateSelectedRegionControls();
    }

    public void SelectLastRegion(string? regionStatus = null)
    {
        SelectedIndex = getCurrentFrame() is { Regions.Count: > 0 } frame ? frame.Regions.Count - 1 : -1;
        if (regionStatus is not null)
        {
            regionStatusText.Text = regionStatus;
        }

        UpdateSelectedRegionControls();
    }

    public void SetRegionStatus(string text)
    {
        regionStatusText.Text = text;
    }

    public bool DuplicateSelected()
    {
        if (!TryGetSelectedRegion(out var frame, out var region))
        {
            setStatus("No selected region");
            return false;
        }

        frame.Regions.Insert(SelectedIndex + 1, regionEditService.Move(region, 8, 8));
        SelectedIndex++;
        redrawRegions();
        UpdateSelectedRegionControls();
        addLog($"duplicated region {SelectedIndex}");
        setStatus("Region duplicated");
        return true;
    }

    public bool DeleteSelected()
    {
        if (getCurrentFrame() is not { } frame || SelectedIndex < 0 || SelectedIndex >= frame.Regions.Count)
        {
            setStatus("No selected region");
            return false;
        }

        frame.Regions.RemoveAt(SelectedIndex);
        SelectedIndex = Math.Min(SelectedIndex, frame.Regions.Count - 1);
        redrawRegions();
        UpdateSelectedRegionControls();
        addLog("deleted region");
        setStatus("Region deleted");
        return true;
    }

    public bool ApplySelectedProperties(bool silent = false)
    {
        if (!TryGetSelectedRegion(out var frame, out var region))
        {
            return false;
        }

        if (!regionEditService.TryUpdateValues(region, regionValuesText.Text, out var updated))
        {
            if (!silent)
            {
                setStatus("Invalid region values");
                addLog($"invalid region values: {regionValuesText.Text}");
            }

            UpdateSelectedRegionControls();
            return false;
        }

        updated = regionEditService.WithLabel(updated, regionLabelText.Text);
        frame.Regions[SelectedIndex] = updated;
        redrawRegions();
        UpdateSelectedRegionControls();
        if (!silent)
        {
            addLog($"updated region {SelectedIndex + 1}");
            setStatus("Region updated");
        }

        return true;
    }

    public void UpdateSelectedRegionControls()
    {
        if (!TryGetSelectedRegion(out _, out var region))
        {
            selectedRegionText.Text = "Selected: --";
            regionLabelText.Text = string.Empty;
            regionValuesText.Text = string.Empty;
            regionLabelText.IsEnabled = false;
            regionValuesText.IsEnabled = false;
            return;
        }

        selectedRegionText.Text = $"Selected: {SelectedIndex + 1} {region.Kind} ({region.CoordinateSystem})";
        regionLabelText.IsEnabled = true;
        regionValuesText.IsEnabled = true;
        regionLabelText.Text = region.Label ?? string.Empty;
        regionValuesText.Text = string.Join(", ", region.Values.Select(value => value.ToString("0.###", CultureInfo.InvariantCulture)));
    }

    private bool TryGetSelectedRegion(out ImageFrameViewModel frame, out Ds9Region region)
    {
        frame = null!;
        region = null!;
        if (getCurrentFrame() is not { } current || SelectedIndex < 0 || SelectedIndex >= current.Regions.Count)
        {
            return false;
        }

        frame = current;
        region = current.Regions[SelectedIndex];
        return true;
    }
}
