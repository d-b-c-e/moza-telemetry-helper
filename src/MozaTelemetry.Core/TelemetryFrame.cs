namespace MozaTelemetry.Core;

// SI units, RPM, normalized pedals, gear -1=reverse / 0=neutral / 1+=forward.
public sealed record TelemetryFrame
{
    public bool IsRaceOn { get; init; } = true;
    public uint TimestampMs { get; init; }
    public float Rpm { get; init; }
    public float MaxRpm { get; init; }
    public float IdleRpm { get; init; }
    public float Speed { get; init; }
    public int Gear { get; init; }
    public float Throttle { get; init; }
    public float Brake { get; init; }
    public float Clutch { get; init; }
    public float Handbrake { get; init; }
    public float Steering { get; init; }
    public float Fuel { get; init; }
    public float Distance { get; init; }
    public float CurrentLap { get; init; }
    public float LastLap { get; init; }
    public float RaceTime { get; init; }
    public ushort LapNumber { get; init; }
    public byte Position { get; init; }
}
