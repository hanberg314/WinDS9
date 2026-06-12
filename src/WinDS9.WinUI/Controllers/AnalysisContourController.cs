using WinDS9.Engine;
using WinDS9.WinUI;

namespace WinDS9.WinUI.Controllers;

internal sealed class AnalysisContourController
{
    private readonly AnalysisService analysisService = new();
    private readonly ContourGenerator contourGenerator = new();
    private readonly Func<ImageFrameViewModel?> getCurrentFrame;
    private readonly Action redrawRegions;
    private readonly Action<string> updateAnalysisSummary;
    private readonly Action<string> setStatus;
    private readonly Action<string> addLog;

    public AnalysisContourController(
        Func<ImageFrameViewModel?> getCurrentFrame,
        Action redrawRegions,
        Action<string> updateAnalysisSummary,
        Action<string> setStatus,
        Action<string> addLog)
    {
        this.getCurrentFrame = getCurrentFrame;
        this.redrawRegions = redrawRegions;
        this.updateAnalysisSummary = updateAnalysisSummary;
        this.setStatus = setStatus;
        this.addLog = addLog;
    }

    public void ExecuteContour()
    {
        if (getCurrentFrame() is not { } frame)
        {
            setStatus("No frame");
            return;
        }

        var levels = contourGenerator.AutoLevels(frame.Image, count: 6, low: frame.LowCut, high: frame.HighCut);
        var contours = contourGenerator.Generate(frame.Image, levels);
        frame.Contours.Clear();
        frame.Contours.AddRange(contours);
        redrawRegions();
        addLog($"contour levels={levels.Count} segments={contours.Count}");
        setStatus($"Contour: {contours.Count} segments");
    }

    public async Task AnalyzeAsync()
    {
        if (getCurrentFrame() is not { } frame)
        {
            setStatus("No frame");
            return;
        }

        setStatus("Analyzing frame");
        var analysis = await Task.Run(() => analysisService.Analyze(frame.Image));
        frame.Analysis = analysis;
        updateAnalysisSummary(analysis.Summary);
        addLog($"analysis {analysis.Summary}");
        setStatus("Analysis updated");
    }
}
