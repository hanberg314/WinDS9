using WinDS9.Engine;

namespace WinDS9.WinUI;

internal sealed class ImageFrameViewModel
{
    public ImageFrameViewModel(int index, LoadedImage image)
    {
        Index = index;
        Image = image;
        FileName = Path.GetFileName(image.FilePath);
        DisplayName = $"{index}: {FileName}";
        LowCut = image.DataMin;
        HighCut = image.DataMax;
    }

    public int Index { get; }

    public LoadedImage Image { get; private set; }

    public string FileName { get; }

    public string DisplayName { get; }

    public string CompactDisplayName => Index.ToString();

    public double LowCut { get; set; }

    public double HighCut { get; set; }

    public List<Ds9Region> Regions { get; } = [];

    public List<ContourSegment> Contours { get; } = [];

    public ImageAnalysisResult? Analysis { get; set; }

    public void ReplaceImage(LoadedImage image)
    {
        Image = image;
        LowCut = image.LowCut;
        HighCut = image.HighCut;
        Contours.Clear();
        Analysis = null;
    }

    public override string ToString() => DisplayName;
}
