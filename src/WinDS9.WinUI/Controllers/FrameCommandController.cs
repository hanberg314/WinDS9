using System.Globalization;
using WinDS9.WinUI;

namespace WinDS9.WinUI.Controllers;

internal sealed class FrameCommandController
{
    private readonly FrameWorkspaceController frameWorkspace;
    private readonly Func<ImageFrameViewModel, Task> selectFrameAsync;

    public FrameCommandController(
        FrameWorkspaceController frameWorkspace,
        Func<ImageFrameViewModel, Task> selectFrameAsync)
    {
        this.frameWorkspace = frameWorkspace;
        this.selectFrameAsync = selectFrameAsync;
    }

    public async Task ApplyCommandAsync(IReadOnlyList<string> args)
    {
        if (frameWorkspace.Count == 0)
        {
            return;
        }

        if (args.Count == 0 || string.Equals(args[0], "next", StringComparison.OrdinalIgnoreCase))
        {
            await SelectIfPresentAsync(frameWorkspace.NextFrame());
            return;
        }

        if (string.Equals(args[0], "prev", StringComparison.OrdinalIgnoreCase))
        {
            await SelectIfPresentAsync(frameWorkspace.PreviousFrame());
            return;
        }

        if (int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var frameNumber))
        {
            await SelectIfPresentAsync(frameWorkspace.FindByIndex(frameNumber));
        }
    }

    private async Task SelectIfPresentAsync(ImageFrameViewModel? frame)
    {
        if (frame is not null)
        {
            await selectFrameAsync(frame);
        }
    }
}
