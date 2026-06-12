using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using WinDS9.WinUI;

namespace WinDS9.WinUI.Controllers;

internal sealed class FrameBlinkController
{
    private readonly FrameWorkspaceController frameWorkspace;
    private readonly AppBarToggleButton blinkButton;
    private readonly DispatcherQueueTimer timer;
    private readonly Func<ImageFrameViewModel, Task> selectFrameAsync;
    private readonly Action<string> setStatus;

    public FrameBlinkController(
        DispatcherQueue dispatcherQueue,
        FrameWorkspaceController frameWorkspace,
        AppBarToggleButton blinkButton,
        Func<ImageFrameViewModel, Task> selectFrameAsync,
        Action<string> setStatus)
    {
        this.frameWorkspace = frameWorkspace;
        this.blinkButton = blinkButton;
        this.selectFrameAsync = selectFrameAsync;
        this.setStatus = setStatus;

        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(700);
        timer.Tick += Timer_Tick;
    }

    public void ToggleRequested()
    {
        if (frameWorkspace.Count < 2)
        {
            Stop();
            setStatus("Blink needs at least two frames");
            return;
        }

        if (blinkButton.IsChecked == true)
        {
            timer.Start();
            setStatus("Blink started");
        }
        else
        {
            timer.Stop();
            setStatus("Blink stopped");
        }
    }

    public void StopIfFrameCountBelowMinimum(int frameCount)
    {
        if (frameCount < 2)
        {
            Stop();
        }
    }

    public void Stop()
    {
        timer.Stop();
        blinkButton.IsChecked = false;
    }

    private async void Timer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (frameWorkspace.Count < 2 || frameWorkspace.CurrentFrame is null)
        {
            Stop();
            return;
        }

        var nextFrame = frameWorkspace.NextFrame();
        if (nextFrame is not null)
        {
            await selectFrameAsync(nextFrame);
        }
    }
}
