using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using MozaTelemetry.Core;

namespace MozaTelemetry.Tests;

public class BridgeTests
{
    private static UdpClient Listener() => new(new IPEndPoint(IPAddress.Loopback, 0));
    private static int Port(UdpClient socket) => ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
    private static int FreePort() { using var socket = Listener(); return Port(socket); }
    private static Task<UdpReceiveResult> Receive(UdpClient socket) => socket.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(4));

    [Fact]
    public async Task RelayPreservesUnknownFieldsAndStopsCleanlyWithReceivePending()
    {
        using var destination = Listener(); using var input = new UdpClient();
        var inputPort = FreePort();
        var bridge = new TelemetryBridge(new() { Mode = TelemetryMode.Fh5Relay, ListenPort = inputPort, OutputPort = Port(destination) });
        bridge.Start();
        try
        {
            var bytes = new byte[324]; bytes[0] = 1; bytes[243] = 198; bytes[323] = 77;
            await input.SendAsync(bytes, new IPEndPoint(IPAddress.Loopback, inputPort));
            Assert.Equal(bytes, (await Receive(destination)).Buffer);
        }
        finally { await bridge.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(4)); }
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian((await Receive(destination)).Buffer));
        using var rebound = new UdpClient(new IPEndPoint(IPAddress.Loopback, inputPort));
    }

    [Theory]
    [InlineData(TelemetryMode.Codemasters, 6500, 3)]
    [InlineData(TelemetryMode.ProjectCars2, 7200, 4)]
    public async Task ConvertsSourcePacketOverRealUdp(TelemetryMode mode, int rpm, int gear)
    {
        using var destination = Listener(); using var input = new UdpClient();
        var inputPort = FreePort();
        await using var bridge = new TelemetryBridge(new() { Mode = mode, ListenPort = inputPort, OutputPort = Port(destination) });
        bridge.Start();
        var bytes = mode == TelemetryMode.Codemasters ? ProtocolTests.CodemastersFixture() : ProtocolTests.ProjectCarsFixture();
        await input.SendAsync(bytes, new IPEndPoint(IPAddress.Loopback, inputPort));
        var packet = (await Receive(destination)).Buffer;
        Assert.Equal(324, packet.Length);
        Assert.Equal(rpm, BinaryPrimitives.ReadSingleLittleEndian(packet.AsSpan(16)));
        Assert.Equal(gear, packet[319]);
    }

    [Fact]
    public async Task InvalidPacketsCannotKeepOldTelemetryAlive()
    {
        using var destination = Listener(); using var input = new UdpClient();
        var inputPort = FreePort(); var endpoint = new IPEndPoint(IPAddress.Loopback, inputPort);
        await using var bridge = new TelemetryBridge(new() { Mode = TelemetryMode.Fh5Relay, ListenPort = inputPort, OutputPort = Port(destination), StaleTimeout = TimeSpan.FromMilliseconds(250) });
        bridge.Start();
        var bytes = new byte[324]; bytes[0] = 1;
        await input.SendAsync(bytes, endpoint); await Receive(destination);
        for (var i = 0; i < 5; i++) { await input.SendAsync(new byte[311], endpoint); await Task.Delay(80); }
        var off = (await Receive(destination)).Buffer;
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(off));
        Assert.True(bridge.Status.Stale); Assert.Equal(5, bridge.Status.Rejected);
        await Task.Delay(150); Assert.Equal(0, destination.Available); // One reset, not stale replay.
        await input.SendAsync(bytes, endpoint); Assert.Equal(1, (await Receive(destination)).Buffer[0]);
    }

    [Fact]
    public async Task OccupiedInputPortFailsBeforeStarting()
    {
        using var occupied = Listener();
        await using var bridge = new TelemetryBridge(new() { Mode = TelemetryMode.Fh5Relay, ListenPort = Port(occupied), OutputPort = FreePort() });
        Assert.Throws<SocketException>(bridge.Start);
    }

    [Fact]
    public async Task ProcessOnlyDoesNotBindInputOrSendPackets()
    {
        using var occupied = Listener();
        await using var bridge = new TelemetryBridge(new() { Mode = TelemetryMode.ProcessOnly, ListenPort = Port(occupied), OutputPort = Port(occupied) });
        bridge.Start(); await bridge.Completion;
        Assert.Equal(0, occupied.Available); Assert.Equal(0, bridge.Status.Sent);
    }

    [Fact]
    public async Task DemoEmitsLiveDashboardAndRaceOffOnStop()
    {
        using var destination = Listener();
        var bridge = new TelemetryBridge(new() { Mode = TelemetryMode.Demo, OutputPort = Port(destination) });
        bridge.Start();
        Assert.Equal(1, (await Receive(destination)).Buffer[0]);
        await bridge.DisposeAsync();
        byte[] last = (await Receive(destination)).Buffer;
        while (destination.Available > 0) last = (await Receive(destination)).Buffer;
        Assert.Equal(0, last[0]);
    }

    [Theory]
    [InlineData("127.0.0.1", 20055, 20055)] [InlineData("not-an-ip", 20777, 20055)]
    [InlineData("127.0.0.1", 0, 20055)] [InlineData("127.0.0.1", 20777, 65536)]
    public void BadEndpointsAndFeedbackLoopsAreRejected(string address, int input, int output) =>
        Assert.Throws<ArgumentException>(() => new BridgeOptions { Mode = TelemetryMode.Fh5Relay, OutputAddress = address, ListenPort = input, OutputPort = output }.Validate());
}
