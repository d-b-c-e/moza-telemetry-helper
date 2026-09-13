using System.Buffers.Binary;
using MozaTelemetry.Core;
using MozaTelemetry.App;

namespace MozaTelemetry.Tests;

public class ProtocolTests
{
    [Fact]
    public void HorizonPacketMatchesWireOffsetsAndUnits()
    {
        var bytes = Fh5Packet.Encode(new TelemetryFrame { TimestampMs = 0x12345678, Rpm = 6500, MaxRpm = 8000, IdleRpm = 900,
            Speed = 40, Gear = 4, Throttle = 1, Brake = 0.5f, Steering = -1, Fuel = 0.75f, CurrentLap = 83.5f, LapNumber = 2 });
        Assert.Equal(324, bytes.Length);
        Assert.Equal(new byte[] { 1, 0, 0, 0, 0x78, 0x56, 0x34, 0x12 }, bytes[..8]);
        Assert.Equal(6500, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(16)));
        Assert.Equal(8000, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(8)));
        Assert.Equal(40, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(256))); // m/s, not km/h
        Assert.All(bytes[232..244], b => Assert.Equal(0, b)); // Horizon gap
        Assert.Equal(0.75f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(288)));
        Assert.Equal(83.5f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(304)));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(312)));
        Assert.Equal(255, bytes[315]); Assert.Equal(128, bytes[316]);
        Assert.Equal(4, bytes[319]); Assert.Equal(-127, unchecked((sbyte)bytes[320]));
    }

    [Theory]
    [InlineData(-1, 0)] [InlineData(0, 11)] [InlineData(1, 1)] [InlineData(6, 6)]
    public void NormalizedGearsMapToForza(int input, byte output) => Assert.Equal(output, Fh5Packet.Encode(new() { Gear = input })[319]);

    [Theory]
    [InlineData(232)] [InlineData(311)] [InlineData(323)] [InlineData(325)] [InlineData(331)] [InlineData(0)]
    public void RelayRejectsOtherForzaLayouts(int length) => Assert.False(Fh5Packet.IsValid(new byte[length]));

    [Fact]
    public void RelayRejectsCorruptDashboard()
    {
        var bytes = new byte[324]; bytes[0] = 5;
        Assert.False(Fh5Packet.IsValid(bytes));
        bytes[0] = 1; Write(bytes, 256, float.NaN);
        Assert.False(Fh5Packet.IsValid(bytes));
    }

    // Independent fixtures written at published source offsets; no encoder/decoder round trip.
    internal static byte[] CodemastersFixture()
    {
        var bytes = new byte[264];
        Write(bytes, 0, 120); Write(bytes, 4, 40); Write(bytes, 8, 1200); Write(bytes, 12, 0.25f);
        Write(bytes, 28, 35); Write(bytes, 116, 0.8f); Write(bytes, 120, -0.25f);
        Write(bytes, 124, 0.2f); Write(bytes, 132, 3); Write(bytes, 148, 650);
        Write(bytes, 180, 30); Write(bytes, 184, 60); Write(bytes, 252, 800); Write(bytes, 256, 90);
        return bytes;
    }

    [Fact]
    public void CodemastersConvertsRpmAndKeepsDistanceSeparateFromProgress()
    {
        Assert.True(Decoders.TryCodemasters(CodemastersFixture(), 10, out var frame));
        Assert.Equal(6500, frame.Rpm); Assert.Equal(8000, frame.MaxRpm); Assert.Equal(900, frame.IdleRpm);
        Assert.Equal(35, frame.Speed); Assert.Equal(3, frame.Gear); Assert.Equal(1200, frame.Distance);
        Assert.Equal(0.5f, frame.Fuel); Assert.Equal(40, frame.CurrentLap);
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 1)] [InlineData(10, -1)]
    public void CodemastersGearEncoding(int raw, int normalized)
    {
        var bytes = CodemastersFixture(); Write(bytes, 132, raw);
        Assert.True(Decoders.TryCodemasters(bytes, 10, out var frame)); Assert.Equal(normalized, frame.Gear);
    }

    [Fact]
    public void CodemastersRejectsBadLengthsNonFiniteDataAndImpossibleRpm()
    {
        Assert.False(Decoders.TryCodemasters(new byte[128], 10, out _));
        var bytes = CodemastersFixture(); Write(bytes, 28, float.NaN);
        Assert.False(Decoders.TryCodemasters(bytes, 10, out _));
        bytes = CodemastersFixture(); Write(bytes, 148, -50);
        Assert.False(Decoders.TryCodemasters(bytes, 10, out _));
        Assert.False(Decoders.TryCodemasters(CodemastersFixture(), float.PositiveInfinity, out _));
    }

    internal static byte[] ProjectCarsFixture()
    {
        var bytes = new byte[556]; bytes[11] = 2; bytes[9] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(40), 7200);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), 8500);
        Write(bytes, 36, 50); Write(bytes, 32, 0.7f); Write(bytes, 48, 1.25f);
        bytes[45] = 0x64; bytes[30] = 255; bytes[29] = 128; bytes[44] = 129; bytes[370] = 255;
        return bytes;
    }

    [Fact]
    public void ProjectCars2UsesPackedFieldsWithoutConfusingGearAndGearCount()
    {
        Assert.True(Decoders.TryProjectCars2(ProjectCarsFixture(), out var frame));
        Assert.Equal(7200, frame.Rpm); Assert.Equal(8500, frame.MaxRpm); Assert.Equal(4, frame.Gear);
        Assert.Equal(50, frame.Speed); Assert.Equal(1250, frame.Distance); Assert.Equal(-1, frame.Steering);
        Assert.Equal(1, frame.Handbrake); Assert.Equal(1, frame.Throttle);
    }

    [Theory]
    [InlineData(0x60, 0)] [InlineData(0x6f, -1)]
    public void ProjectCars2NeutralAndReverse(int raw, int expected)
    {
        var bytes = ProjectCarsFixture(); bytes[45] = (byte)raw;
        Assert.True(Decoders.TryProjectCars2(bytes, out var frame)); Assert.Equal(expected, frame.Gear);
    }

    [Fact]
    public void ProjectCars2IgnoresOtherPacketTypesVersionsAndInvalidParticipant()
    {
        var bytes = ProjectCarsFixture(); bytes[10] = 1;
        Assert.False(Decoders.TryProjectCars2(bytes, out _));
        bytes[10] = 0; bytes[11] = 1;
        Assert.False(Decoders.TryProjectCars2(bytes, out _));
        bytes[11] = 2; bytes[12] = 255;
        Assert.False(Decoders.TryProjectCars2(bytes, out _));
        Assert.False(Decoders.TryProjectCars2(new byte[559], out _));
    }

    [Theory]
    [InlineData("ForzaHorizon5", "ForzaHorizon5.exe")]
    [InlineData("Game-Win64-Shipping.EXE", "Game-Win64-Shipping.exe")]
    [InlineData(" My Game ", "My Game.exe")]
    public void ExecutableNamesNormalize(string input, string expected) => Assert.Equal(expected, ProcessIdentity.NormalizeName(input));

    [Theory]
    [InlineData("")] [InlineData("../game")] [InlineData("C:\\game.exe")] [InlineData("name:stream")]
    [InlineData("CON.exe")] [InlineData("NUL.other.exe")] [InlineData("game." )] [InlineData("a/b")] [InlineData("COM1")]
    public void UnsafeExecutableNamesAreRejected(string input) => Assert.Throws<ArgumentException>(() => ProcessIdentity.NormalizeName(input));

    [Fact]
    public void WindowsStartupQuotesPathsWithSpacesAndStartsOnlyTheTrayApp()
    {
        Assert.Equal("\"C:\\Games and Tools\\MOZA\\MozaTelemetryHelper.exe\" --tray", WindowsStartup.StartupCommand(@"C:\Games and Tools\MOZA\MozaTelemetryHelper.exe"));
        Assert.Throws<ArgumentException>(() => WindowsStartup.StartupCommand("relative.exe"));
        Assert.Throws<ArgumentException>(() => WindowsStartup.StartupCommand("C:\\bad\"path.exe"));
    }

    internal static void Write(byte[] bytes, int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset), value);
}
