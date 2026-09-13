using System.Buffers.Binary;

namespace MozaTelemetry.Core;

public static class Fh5Packet
{
    public const int Length = 324;
    public static float ReadFloat(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset, 4));
    public static void WriteFloat(Span<byte> bytes, int offset, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(bytes.Slice(offset, 4), float.IsFinite(value) ? value : 0);

    public static bool IsValid(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Length || BinaryPrimitives.ReadInt32LittleEndian(bytes) is not (0 or 1)) return false;
        return IsFiniteDashboard(bytes);
    }

    private static bool IsFiniteDashboard(ReadOnlySpan<byte> bytes) =>
        float.IsFinite(ReadFloat(bytes, 8)) && float.IsFinite(ReadFloat(bytes, 12)) &&
        float.IsFinite(ReadFloat(bytes, 16)) && float.IsFinite(ReadFloat(bytes, 256));

    public static byte[] Encode(TelemetryFrame frame)
    {
        var bytes = new byte[Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, frame.IsRaceOn ? 1 : 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), frame.TimestampMs);
        WriteFloat(bytes, 8, frame.MaxRpm);
        WriteFloat(bytes, 12, frame.IdleRpm);
        WriteFloat(bytes, 16, frame.Rpm);
        // Horizon inserts 12 bytes after the 232-byte Sled block. Unavailable fields remain zero.
        WriteFloat(bytes, 256, frame.Speed);
        WriteFloat(bytes, 288, frame.Fuel);
        WriteFloat(bytes, 292, frame.Distance);
        WriteFloat(bytes, 300, frame.LastLap);
        WriteFloat(bytes, 304, frame.CurrentLap);
        WriteFloat(bytes, 308, frame.RaceTime);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(312), frame.LapNumber);
        bytes[314] = frame.Position;
        bytes[315] = Pedal(frame.Throttle);
        bytes[316] = Pedal(frame.Brake);
        bytes[317] = Pedal(frame.Clutch);
        bytes[318] = Pedal(frame.Handbrake);
        bytes[319] = frame.Gear switch { -1 => 0, 0 => 11, _ => (byte)Math.Clamp(frame.Gear, 1, 10) };
        bytes[320] = unchecked((byte)(sbyte)MathF.Round(Math.Clamp(Finite(frame.Steering), -1, 1) * 127));
        return bytes;
    }

    public static TelemetryFrame Dashboard(ReadOnlySpan<byte> bytes) => new()
    {
        IsRaceOn = BinaryPrimitives.ReadInt32LittleEndian(bytes) == 1,
        Rpm = ReadFloat(bytes, 16), MaxRpm = ReadFloat(bytes, 8), Speed = ReadFloat(bytes, 256),
        Gear = bytes[319] switch { 0 => -1, 11 => 0, var gear => gear }
    };

    private static float Finite(float value) => float.IsFinite(value) ? value : 0;
    private static byte Pedal(float value) => (byte)MathF.Round(Math.Clamp(Finite(value), 0, 1) * 255);
}
