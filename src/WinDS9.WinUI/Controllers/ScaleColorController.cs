using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using WinDS9.Engine;
using WinDS9.WinUI;

namespace WinDS9.WinUI.Controllers;

internal enum CutInputUnit
{
    Value,
    Percent
}

internal sealed class ScaleColorController
{
    private readonly ComboBox stretchCombo;
    private readonly ComboBox cutUnitCombo;
    private readonly ComboBox colorMapCombo;
    private readonly Slider lowCutSlider;
    private readonly Slider highCutSlider;
    private readonly TextBox lowCutValueText;
    private readonly TextBox highCutValueText;
    private readonly TextBlock cutText;
    private readonly Func<ImageFrameViewModel?> getCurrentFrame;

    public ScaleColorController(
        ComboBox stretchCombo,
        ComboBox cutUnitCombo,
        ComboBox colorMapCombo,
        Slider lowCutSlider,
        Slider highCutSlider,
        TextBox lowCutValueText,
        TextBox highCutValueText,
        TextBlock cutText,
        Func<ImageFrameViewModel?> getCurrentFrame)
    {
        this.stretchCombo = stretchCombo;
        this.cutUnitCombo = cutUnitCombo;
        this.colorMapCombo = colorMapCombo;
        this.lowCutSlider = lowCutSlider;
        this.highCutSlider = highCutSlider;
        this.lowCutValueText = lowCutValueText;
        this.highCutValueText = highCutValueText;
        this.cutText = cutText;
        this.getCurrentFrame = getCurrentFrame;
    }

    public bool IsUpdating { get; private set; }

    public void Initialize()
    {
        stretchCombo.ItemsSource = Enum.GetNames<ImageStretch>();
        stretchCombo.SelectedItem = ImageStretch.Linear.ToString();
        cutUnitCombo.ItemsSource = Enum.GetNames<CutInputUnit>();
        cutUnitCombo.SelectedItem = CutInputUnit.Value.ToString();
        colorMapCombo.ItemsSource = Enum.GetNames<ImageColorMap>();
        colorMapCombo.SelectedItem = ImageColorMap.Gray.ToString();
        ConfigureCutSliders();
    }

    public void Reset()
    {
        IsUpdating = true;
        ConfigureCutSliders();
        lowCutSlider.Value = 0;
        highCutSlider.Value = 1;
        lowCutValueText.Text = "--";
        highCutValueText.Text = "--";
        cutText.Text = "--";
        IsUpdating = false;
    }

    public void UpdateScaleControls(LoadedImage image)
    {
        var (min, max) = DataRange(image);
        var frame = getCurrentFrame();
        var low = Math.Clamp(frame?.LowCut ?? min, min, max);
        var high = Math.Clamp(frame?.HighCut ?? max, min, max);
        if (high <= low)
        {
            var step = ComputeDataCutStep(image);
            high = Math.Min(max, low + step);
            if (high <= low)
            {
                low = Math.Max(min, high - step);
            }
        }

        if (frame is not null)
        {
            frame.LowCut = low;
            frame.HighCut = high;
        }

        IsUpdating = true;
        ConfigureCutSliders();
        lowCutSlider.Value = CutToSliderPosition(image, low);
        highCutSlider.Value = CutToSliderPosition(image, high);
        IsUpdating = false;

        UpdateCutControls(low, high);
    }

    public void UpdateCutControls(double low, double high)
    {
        IsUpdating = true;
        if (getCurrentFrame() is { } frame)
        {
            lowCutSlider.Value = CutToSliderPosition(frame.Image, low);
            highCutSlider.Value = CutToSliderPosition(frame.Image, high);
        }

        IsUpdating = false;
        lowCutValueText.Text = FormatCutInput(low);
        highCutValueText.Text = FormatCutInput(high);
        cutText.Text = FormatCut(low, high);
    }

    public bool TryHandleSliderChange(object sender)
    {
        if (IsUpdating || getCurrentFrame() is not { } frame)
        {
            return false;
        }

        var image = frame.Image;
        const double minimumPositionStep = 0.001;
        var lowPosition = lowCutSlider.Value;
        var highPosition = highCutSlider.Value;

        if (highPosition <= lowPosition)
        {
            if (ReferenceEquals(sender, lowCutSlider))
            {
                highPosition = Math.Min(highCutSlider.Maximum, lowPosition + minimumPositionStep);
                if (highPosition <= lowPosition)
                {
                    lowPosition = Math.Max(lowCutSlider.Minimum, highPosition - minimumPositionStep);
                }
            }
            else
            {
                lowPosition = Math.Max(lowCutSlider.Minimum, highPosition - minimumPositionStep);
                if (highPosition <= lowPosition)
                {
                    highPosition = Math.Min(highCutSlider.Maximum, lowPosition + minimumPositionStep);
                }
            }
        }

        frame.LowCut = SliderPositionToCut(image, lowPosition);
        frame.HighCut = SliderPositionToCut(image, highPosition);
        UpdateCutControls(frame.LowCut, frame.HighCut);
        return true;
    }

    public bool TryApplyCutInput(object sender, out string? errorMessage)
    {
        errorMessage = null;
        if (IsUpdating || getCurrentFrame() is not { } frame || sender is not TextBox textBox)
        {
            return false;
        }

        var image = frame.Image;
        var isLowInput = ReferenceEquals(textBox, lowCutValueText);
        if (!TryParseCutInput(image, textBox.Text, out var enteredCut))
        {
            errorMessage = $"invalid {(isLowInput ? "low" : "high")} cut: {textBox.Text}";
            UpdateCutControls(frame.LowCut, frame.HighCut);
            return false;
        }

        var (min, max) = DataRange(image);
        var low = frame.LowCut;
        var high = frame.HighCut;
        if (isLowInput)
        {
            low = Math.Clamp(enteredCut, min, max);
        }
        else
        {
            high = Math.Clamp(enteredCut, min, max);
        }

        var step = ComputeDataCutStep(image);
        if (high <= low)
        {
            if (isLowInput)
            {
                high = Math.Min(max, low + step);
                if (high <= low)
                {
                    low = Math.Max(min, high - step);
                }
            }
            else
            {
                low = Math.Max(min, high - step);
                if (high <= low)
                {
                    high = Math.Min(max, low + step);
                }
            }
        }

        frame.LowCut = low;
        frame.HighCut = high;
        UpdateCutControls(low, high);
        return true;
    }

    public ImageStretch CurrentStretch()
    {
        return Enum.TryParse<ImageStretch>(stretchCombo.SelectedItem?.ToString(), out var selected)
            ? selected
            : ImageStretch.Linear;
    }

    public ImageColorMap CurrentColorMap()
    {
        return Enum.TryParse<ImageColorMap>(colorMapCombo.SelectedItem?.ToString(), out var selected)
            ? selected
            : ImageColorMap.Gray;
    }

    public static string FormatCut(double low, double high) => $"{FormatScalar(low)} - {FormatScalar(high)}";

    public static string FormatScalar(double value)
    {
        if (!double.IsFinite(value))
        {
            return "--";
        }

        var abs = Math.Abs(value);
        return abs >= 1_000_000 || (abs > 0 && abs < 0.001)
            ? value.ToString("0.###e+0")
            : value.ToString("0.###");
    }

    private void ConfigureCutSliders()
    {
        lowCutSlider.Minimum = 0;
        lowCutSlider.Maximum = 1;
        lowCutSlider.SmallChange = 0.001;
        lowCutSlider.LargeChange = 0.05;
        lowCutSlider.StepFrequency = 0.001;
        highCutSlider.Minimum = 0;
        highCutSlider.Maximum = 1;
        highCutSlider.SmallChange = 0.001;
        highCutSlider.LargeChange = 0.05;
        highCutSlider.StepFrequency = 0.001;
    }

    private static (double Min, double Max) DataRange(LoadedImage image)
    {
        var min = image.DataMin;
        var max = image.DataMax;
        if (!double.IsFinite(min) || !double.IsFinite(max) || max <= min)
        {
            min = 0;
            max = 1;
        }

        return (min, max);
    }

    private static double ComputeDataCutStep(LoadedImage image)
    {
        var range = image.DataMax - image.DataMin;
        return double.IsFinite(range) && range > 0 ? range / 1000 : 0.001;
    }

    private double CutToSliderPosition(LoadedImage image, double cut)
    {
        var normalized = NormalizeDataCut(image, cut);
        return CurrentStretch() switch
        {
            ImageStretch.Log => Math.Log10(1 + 999 * normalized) / Math.Log10(1000),
            ImageStretch.Sqrt => Math.Sqrt(normalized),
            _ => normalized
        };
    }

    private double SliderPositionToCut(LoadedImage image, double position)
    {
        var clamped = Math.Clamp(position, 0, 1);
        var normalized = CurrentStretch() switch
        {
            ImageStretch.Log => (Math.Pow(1000, clamped) - 1) / 999,
            ImageStretch.Sqrt => clamped * clamped,
            _ => clamped
        };

        var (min, max) = DataRange(image);
        return min + normalized * (max - min);
    }

    private static double NormalizeDataCut(LoadedImage image, double cut)
    {
        var (min, max) = DataRange(image);
        return Math.Clamp((cut - min) / (max - min), 0, 1);
    }

    private bool TryParseCutInput(LoadedImage image, string text, out double cut)
    {
        cut = 0;
        var trimmed = text.Trim();
        if (trimmed.EndsWith('%'))
        {
            trimmed = trimmed[..^1].Trim();
        }

        if (!TryParseFlexibleDouble(trimmed, out var parsed))
        {
            return false;
        }

        if (CurrentCutInputUnit() == CutInputUnit.Percent)
        {
            cut = SliderPositionToCut(image, Math.Clamp(parsed, 0, 100) / 100);
        }
        else
        {
            cut = parsed;
        }

        return double.IsFinite(cut);
    }

    private static bool TryParseFlexibleDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private string FormatCutInput(double cut)
    {
        if (getCurrentFrame() is { } frame && CurrentCutInputUnit() == CutInputUnit.Percent)
        {
            return $"{CutToSliderPosition(frame.Image, cut) * 100:0.###}%";
        }

        return FormatScalar(cut);
    }

    private CutInputUnit CurrentCutInputUnit()
    {
        return Enum.TryParse<CutInputUnit>(cutUnitCombo.SelectedItem?.ToString(), out var selected)
            ? selected
            : CutInputUnit.Value;
    }
}
