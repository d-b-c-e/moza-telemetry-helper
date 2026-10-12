using System.Globalization;
using System.Text.Json;
using MozaTelemetry.Core;

namespace MozaTelemetry.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Keep this handle alive until process exit; installers must also detect headless sessions.
        using var installationLock = new Mutex(false, UpdatePackage.AppMutex);
        if (args.Length == 2 && args[0] == "--preset-check") return PresetCheck(args[1]);
        if (args.Contains("--headless")) return RunHeadlessAsync(args).GetAwaiter().GetResult();
        var startInTray = args.Contains("--tray");
        // A sign-in (--tray) launch stays quiet if the app is already open; any other launch shows the existing window.
        using var instance = SingleInstance.TryAcquire(SingleInstance.WindowName, showExisting: !startInTray);
        if (instance == null) return 0;
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(instance, startInTray));
        return 0;
    }

    /// <summary>Read-only: writes Pit House's expected preset reaction to an identity as JSON on stdout. Starts nothing.</summary>
    private static int PresetCheck(string identity)
    {
        var prediction = PitHousePresets.Predict(identity);
        Console.Out.WriteLine(JsonSerializer.Serialize(new { Outlook = prediction.Outlook.ToString(), prediction.Current, prediction.Predicted, prediction.Reason, prediction.Summary }));
        return prediction.Outlook == PresetOutlook.Unknown ? 1 : 0;
    }

    private static async Task<int> RunHeadlessAsync(string[] args)
    {
        string? report = null;
        try
        {
            var values = new Dictionary<string, string>();
            var flags = new HashSet<string>();
            var knownValues = new[] { "--seconds", "--process", "--mode", "--listen", "--listen-port", "--output", "--output-port", "--rpm-scale", "--report" };
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] is "--headless" or "--no-process") { flags.Add(args[i]); continue; }
                if (!knownValues.Contains(args[i]) || i + 1 >= args.Length) throw new ArgumentException($"Unknown or incomplete option: {args[i]}");
                values.Add(args[i], args[++i]);
            }
            string Value(string key, string fallback) => values.GetValueOrDefault(key, fallback);
            int Number(string key, int fallback) => int.Parse(Value(key, fallback.ToString()), CultureInfo.InvariantCulture);
            report = values.GetValueOrDefault("--report");
            var seconds = Number("--seconds", 30);
            if (seconds is < 1 or > 86400) throw new ArgumentException("Duration must be 1 to 86400 seconds.");
            var settings = new AppSettings
            {
                ProcessName = Value("--process", "ForzaHorizon5.exe"), RunProcess = !flags.Contains("--no-process"),
                Bridge = new BridgeOptions
                {
                    Mode = Enum.Parse<TelemetryMode>(Value("--mode", "ProcessOnly"), true),
                    ListenAddress = Value("--listen", "127.0.0.1"), ListenPort = Number("--listen-port", 20777),
                    OutputAddress = Value("--output", "127.0.0.1"), OutputPort = Number("--output-port", 20055),
                    RpmScale = float.Parse(Value("--rpm-scale", "10"), CultureInfo.InvariantCulture)
                }
            };
            var prediction = settings.RunProcess ? PitHousePresets.Predict(settings.ProcessName) : null;
            var before = settings.RunProcess ? PitHousePresets.Snapshot() : null;
            await using var session = new HelperSession();
            await session.StartAsync(settings);
            var childId = session.Identity.ProcessId;
            if (report != null) File.WriteAllText(report, JsonSerializer.Serialize(new { State = "Running", ChildId = childId, PresetPrediction = prediction?.Summary }));
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            var status = session.Bridge?.Status;
            var unexpectedExit = settings.RunProcess && !session.Identity.IsRunning;
            await session.DisposeAsync();
            var presetChange = before is null ? null : PitHousePresets.DescribeChange(before, PitHousePresets.Snapshot());
            if (report != null) File.WriteAllText(report, JsonSerializer.Serialize(new { State = "Stopped", ChildId = childId, Status = status, UnexpectedExit = unexpectedExit, PresetPrediction = prediction?.Summary, PresetChange = presetChange }));
            return status?.Error == null && !unexpectedExit ? 0 : 1;
        }
        catch (Exception exception)
        {
            if (report != null) File.WriteAllText(report, JsonSerializer.Serialize(new { State = "Error", Error = exception.Message }));
            return 1;
        }
    }
}
