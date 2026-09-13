using System.Buffers.Binary;

namespace MozaTelemetry.Core;

public static class Decoders
{
    // Codemasters legacy extradata=3: 66 little-endian floats. Not modern F1 / EA WRC.
    // Engine values in the DiRT family are RPM / 10; expose the scale for game variants.
    public static bool TryCodemasters(ReadOnlySpan<byte> bytes, float rpmScale, out TelemetryFrame frame)
    {
        frame = new();
        if (bytes.Length != 264 || !float.IsFinite(rpmScale) || rpmScale <= 0) return false;
        for (var offset = 0; offset < bytes.Length; offset += 4)
            if (!float.IsFinite(Fh5Packet.ReadFloat(bytes, offset))) return false;
        float gear = Fh5Packet.ReadFloat(bytes, 132);
        float rpm = Fh5Packet.ReadFloat(bytes, 148) * rpmScale;
        float maxRpm = Fh5Packet.ReadFloat(bytes, 252) * rpmScale;
        if (gear != MathF.Truncate(gear) || gear < 0 || gear > 10 || rpm < 0 || rpm > 100000 || maxRpm <= 0 || maxRpm > 100000)
            return false;
        float fuelCapacity = Fh5Packet.ReadFloat(bytes, 184);
        frame = new()
        {
            Rpm = rpm, MaxRpm = maxRpm, IdleRpm = Fh5Packet.ReadFloat(bytes, 256) * rpmScale,
            Speed = Math.Abs(Fh5Packet.ReadFloat(bytes, 28)),
            Gear = gear == 10 ? -1 : (int)gear,
            Throttle = Fh5Packet.ReadFloat(bytes, 116), Steering = Fh5Packet.ReadFloat(bytes, 120),
            Brake = Fh5Packet.ReadFloat(bytes, 124), Clutch = Fh5Packet.ReadFloat(bytes, 128),
            Fuel = fuelCapacity > 0 ? Math.Clamp(Fh5Packet.ReadFloat(bytes, 180) / fuelCapacity, 0, 1) : 0,
            Distance = Math.Max(0, Fh5Packet.ReadFloat(bytes, 8)),
            CurrentLap = Math.Max(0, Fh5Packet.ReadFloat(bytes, 4)),
            LastLap = Math.Max(0, Fh5Packet.ReadFloat(bytes, 248)),
            RaceTime = Math.Max(0, Fh5Packet.ReadFloat(bytes, 0)),
            LapNumber = (ushort)Math.Clamp(Fh5Packet.ReadFloat(bytes, 144), 0, ushort.MaxValue),
            Position = (byte)Math.Clamp(Fh5Packet.ReadFloat(bytes, 156), 0, byte.MaxValue)
        };
        return true;
    }

    // SMS UDP car-physics v2 only (556 bytes). Other packet categories are not car samples.
    public static bool TryProjectCars2(ReadOnlySpan<byte> bytes, out TelemetryFrame frame)
    {
        frame = new();
        if (bytes.Length != 556 || bytes[10] != 0 || bytes[11] != 2 || unchecked((sbyte)bytes[12]) < 0) return false;
        var speed = Fh5Packet.ReadFloat(bytes, 36);
        var fuel = Fh5Packet.ReadFloat(bytes, 32);
        var odometer = Fh5Packet.ReadFloat(bytes, 48);
        var gear = bytes[45] & 0x0f;
        if (!float.IsFinite(speed) || !float.IsFinite(fuel) || !float.IsFinite(odometer) || gear is > 10 and < 15) return false;
        frame = new()
        {
            Rpm = BinaryPrimitives.ReadUInt16LittleEndian(bytes[40..]),
            MaxRpm = BinaryPrimitives.ReadUInt16LittleEndian(bytes[42..]),
            Speed = Math.Abs(speed), Fuel = Math.Clamp(fuel, 0, 1), Distance = Math.Max(0, odometer * 1000),
            Gear = gear == 15 ? -1 : gear,
            Throttle = bytes[30] / 255f, Brake = bytes[29] / 255f, Clutch = bytes[31] / 255f,
            Steering = Math.Clamp(unchecked((sbyte)bytes[44]) / 127f, -1, 1), Handbrake = bytes[370] / 255f
        };
        return true;
    }
}
