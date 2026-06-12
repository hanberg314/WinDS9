namespace WinDS9.Engine;

public interface IRasterRenderer
{
    RenderedRaster Render(
        LoadedImage image,
        ImageStretch stretch,
        ImageColorMap colorMap = ImageColorMap.Gray,
        double? lowCut = null,
        double? highCut = null);
}
