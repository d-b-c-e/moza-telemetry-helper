namespace MozaTelemetry.App;

public enum ToggleResult { Ignored, Started, Stopped, Unavailable }

// Called on the UI thread; repeated presses during an asynchronous transition are ignored.
public sealed class SessionToggle(Func<bool> blocked, Func<bool> running, Func<bool> canStart, Func<Task> start, Func<Task> stop)
{
    private bool busy;
    public async Task<ToggleResult> ToggleAsync()
    {
        if (busy || blocked()) return ToggleResult.Ignored;
        busy = true;
        try
        {
            if (running())
            {
                await stop();
                return running() ? ToggleResult.Unavailable : ToggleResult.Stopped;
            }
            if (!canStart()) return ToggleResult.Unavailable;
            await start();
            return running() ? ToggleResult.Started : ToggleResult.Unavailable;
        }
        finally { busy = false; }
    }
}
