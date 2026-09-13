using System.Diagnostics;

// No window, telemetry, input, or game code. Only an owned lifetime and a chosen filename.
if (args.Length != 4 || !int.TryParse(args[0], out var parentId) || !long.TryParse(args[1], out var parentTicks)) return 2;
try
{
    using var parent = Process.GetProcessById(parentId);
    if (parent.StartTime.ToUniversalTime().Ticks != parentTicks) return 3;
    _ = parent.Handle; // Keep the original process object even if its PID is later reused.
    using var stop = EventWaitHandle.OpenExisting(args[2]);
    using var ready = EventWaitHandle.OpenExisting(args[3]);
    ready.Set();
    while (!stop.WaitOne(500)) if (parent.HasExited) return 0;
    return 0;
}
catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or WaitHandleCannotBeOpenedException or System.ComponentModel.Win32Exception)
{
    return 4;
}
