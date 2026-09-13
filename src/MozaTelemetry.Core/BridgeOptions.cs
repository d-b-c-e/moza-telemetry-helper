using System.Net;
using System.Net.Sockets;

namespace MozaTelemetry.Core;

public enum TelemetryMode { ProcessOnly, Fh5Relay, Codemasters, ProjectCars2, Demo }

public sealed record BridgeOptions
{
    public TelemetryMode Mode { get; init; } = TelemetryMode.ProcessOnly;
    public string ListenAddress { get; init; } = "127.0.0.1";
    public int ListenPort { get; init; } = 20777;
    public string OutputAddress { get; init; } = "127.0.0.1";
    public int OutputPort { get; init; } = 20055;
    public float RpmScale { get; init; } = 10;
    public TimeSpan StaleTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public void Validate()
    {
        if (!Enum.IsDefined(Mode)) throw new ArgumentException("Unknown telemetry mode.");
        if (Mode == TelemetryMode.ProcessOnly) return;
        if (!IPAddress.TryParse(OutputAddress, out var output) || output.AddressFamily != AddressFamily.InterNetwork || output.Equals(IPAddress.Any) || output.Equals(IPAddress.Broadcast))
            throw new ArgumentException("Output must be a unicast IPv4 address (usually 127.0.0.1).");
        if (!IPAddress.TryParse(ListenAddress, out var input) || input.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Listen address must be IPv4; use 0.0.0.0 for broadcast reception.");
        if (ListenPort is < 1 or > 65535 || OutputPort is < 1 or > 65535)
            throw new ArgumentException("Ports must be between 1 and 65535.");
        if (Mode != TelemetryMode.Demo && ListenPort == OutputPort)
            throw new ArgumentException("Use different input and output ports to prevent UDP loops and binding conflicts.");
        if (!float.IsFinite(RpmScale) || RpmScale is <= 0 or > 1000)
            throw new ArgumentException("RPM multiplier must be greater than zero and at most 1000.");
        if (StaleTimeout < TimeSpan.FromMilliseconds(250)) throw new ArgumentException("Stale timeout must be at least 250 ms.");
    }
}
