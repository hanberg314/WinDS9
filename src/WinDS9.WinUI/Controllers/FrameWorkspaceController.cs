using System.Collections.ObjectModel;
using WinDS9.Engine;

namespace WinDS9.WinUI.Controllers;

internal sealed class FrameWorkspaceController
{
    public ObservableCollection<ImageFrameViewModel> Frames { get; } = [];

    public ImageFrameViewModel? CurrentFrame { get; private set; }

    public int Count => Frames.Count;

    public IReadOnlyList<ImageFrameViewModel> AddImages(IReadOnlyList<LoadedImage> images)
    {
        var added = new List<ImageFrameViewModel>(images.Count);
        foreach (var image in images)
        {
            var frame = new ImageFrameViewModel(Frames.Count + 1, image);
            Frames.Add(frame);
            added.Add(frame);
        }

        if (added.Count > 0)
        {
            CurrentFrame = added[0];
        }

        return added;
    }

    public bool Contains(ImageFrameViewModel frame) => Frames.Contains(frame);

    public bool IsCurrent(ImageFrameViewModel frame) => ReferenceEquals(CurrentFrame, frame);

    public void Select(ImageFrameViewModel frame)
    {
        if (!Frames.Contains(frame))
        {
            return;
        }

        CurrentFrame = frame;
    }

    public ImageFrameViewModel? NextFrame()
    {
        if (Frames.Count == 0)
        {
            return null;
        }

        var index = CurrentFrame is null ? 0 : (Frames.IndexOf(CurrentFrame) + 1) % Frames.Count;
        return Frames[index];
    }

    public ImageFrameViewModel? PreviousFrame()
    {
        if (Frames.Count == 0)
        {
            return null;
        }

        var index = CurrentFrame is null ? 0 : Frames.IndexOf(CurrentFrame) - 1;
        if (index < 0)
        {
            index = Frames.Count - 1;
        }

        return Frames[index];
    }

    public ImageFrameViewModel? FindByIndex(int frameIndex)
    {
        return Frames.FirstOrDefault(candidate => candidate.Index == frameIndex);
    }

    public FrameCloseDecision Close(ImageFrameViewModel frame)
    {
        var index = Frames.IndexOf(frame);
        if (index < 0)
        {
            return new FrameCloseDecision(false, false, false, null, Frames.Count);
        }

        var wasCurrent = IsCurrent(frame);
        Frames.RemoveAt(index);

        if (Frames.Count == 0)
        {
            CurrentFrame = null;
            return new FrameCloseDecision(true, wasCurrent, true, null, 0);
        }

        ImageFrameViewModel? nextFrame = null;
        if (wasCurrent)
        {
            nextFrame = Frames[Math.Min(index, Frames.Count - 1)];
            CurrentFrame = null;
        }

        return new FrameCloseDecision(true, wasCurrent, false, nextFrame, Frames.Count);
    }

    public void ClearSelection()
    {
        CurrentFrame = null;
    }
}

internal sealed record FrameCloseDecision(
    bool Removed,
    bool WasCurrent,
    bool IsEmpty,
    ImageFrameViewModel? NextFrame,
    int RemainingFrameCount);
