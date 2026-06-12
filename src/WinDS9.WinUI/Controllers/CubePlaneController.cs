using Microsoft.UI.Xaml.Controls;
using WinDS9.Engine;
using WinDS9.WinUI;

namespace WinDS9.WinUI.Controllers;

internal sealed class CubePlaneController
{
    private readonly TextBlock cubeText;
    private readonly Slider planeSlider;
    private readonly Button planePreviousButton;
    private readonly Button planeNextButton;
    private readonly TextBlock planeText;
    private readonly Func<ImageFrameViewModel?> getCurrentFrame;

    public CubePlaneController(
        TextBlock cubeText,
        Slider planeSlider,
        Button planePreviousButton,
        Button planeNextButton,
        TextBlock planeText,
        Func<ImageFrameViewModel?> getCurrentFrame)
    {
        this.cubeText = cubeText;
        this.planeSlider = planeSlider;
        this.planePreviousButton = planePreviousButton;
        this.planeNextButton = planeNextButton;
        this.planeText = planeText;
        this.getCurrentFrame = getCurrentFrame;
    }

    public bool IsUpdating { get; private set; }

    public void Reset()
    {
        cubeText.Text = "2D";
        IsUpdating = true;
        planeSlider.Minimum = 1;
        planeSlider.Maximum = 1;
        planeSlider.StepFrequency = 1;
        planeSlider.Value = 1;
        planeSlider.IsEnabled = false;
        planePreviousButton.IsEnabled = false;
        planeNextButton.IsEnabled = false;
        planeText.Text = "2D";
        IsUpdating = false;
    }

    public void Update(LoadedImage image)
    {
        var depth = Math.Max(1, image.CubeDepth);
        var isCube = image.CubeDepth > 1;
        var currentPlane = Math.Clamp(image.PlaneIndex + 1, 1, depth);

        cubeText.Text = isCube
            ? $"plane {currentPlane} / {depth}"
            : "2D";

        IsUpdating = true;
        planeSlider.Minimum = 1;
        planeSlider.Maximum = depth;
        planeSlider.StepFrequency = 1;
        planeSlider.Value = currentPlane;
        planeSlider.IsEnabled = isCube;
        planePreviousButton.IsEnabled = isCube && image.PlaneIndex > 0;
        planeNextButton.IsEnabled = isCube && image.PlaneIndex < depth - 1;
        planeText.Text = isCube
            ? $"plane {currentPlane} / {depth}, HDU {image.HduIndex}"
            : "2D";
        IsUpdating = false;
    }

    public int? PreviousPlane()
    {
        var image = getCurrentFrame()?.Image;
        return image is { CubeDepth: > 1 }
            ? image.PlaneIndex - 1
            : null;
    }

    public int? NextPlane()
    {
        var image = getCurrentFrame()?.Image;
        return image is { CubeDepth: > 1 }
            ? image.PlaneIndex + 1
            : null;
    }

    public int? PlaneFromSliderValue(double value)
    {
        if (IsUpdating || getCurrentFrame()?.Image is not { CubeDepth: > 1 } image)
        {
            return null;
        }

        return Math.Clamp((int)Math.Round(value) - 1, 0, image.CubeDepth - 1);
    }
}
