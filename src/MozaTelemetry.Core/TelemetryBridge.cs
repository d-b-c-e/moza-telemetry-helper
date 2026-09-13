using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace MozaTelemetry.Core;

public sealed record BridgeStatus(long Received = 0, long Sent = 0, long Rejected = 0, long SendErrors = 0,
    DateTimeOffset? LastReceived = null, TelemetryFrame? Dashboard = null, bool Stale = false, string? Error = null);

public sealed class TelemetryBridge : IAsyncDisposable
{
    private readonly BridgeOptions options;
    private readonly CancellationTokenSource cancellation = new();
    private readonly UdpClient sender = new(AddressFamily.InterNetwork);
    private UdpClient? receiver;
    private Task? loop;
    private BridgeStatus status = new();
    public BridgeStatus Status => Volatile.Read(ref status);
    public Task Completion => loop ?? Task.CompletedTask;

    public TelemetryBridge(BridgeOptions options)
    {
        options.Validate();
        this.options = options;
    }

    public void Start()
    {
        if (loop != null) throw new InvalidOperationException("Bridge already started.");
        if (options.Mode == TelemetryMode.ProcessOnly) { loop = Task.CompletedTask; return; }
        if (options.Mode != TelemetryMode.Demo)
        {
            receiver = new UdpClient(AddressFamily.InterNetwork);
            receiver.ExclusiveAddressUse = true;
            try { receiver.Client.Bind(new IPEndPoint(IPAddress.Parse(options.ListenAddress), options.ListenPort)); }
            catch { receiver.Dispose(); receiver = null; throw; }
        }
        loop = RunAsync();
    }

    private async Task RunAsync()
    {
        var token = cancellation.Token;
        var clock = Stopwatch.StartNew();
        double? lastAccepted = null;
        var sampleActive = false;
        Task<UdpReceiveResult>? pending = receiver?.ReceiveAsync(token).AsTask();
        Task tick = Task.Delay(options.Mode == TelemetryMode.Demo ? 16 : 100, token);
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.WhenAny(pending is null ? [tick] : [pending, tick]);
                token.ThrowIfCancellationRequested();
                if (pending?.IsCompleted == true)
                {
                    var packet = (await pending).Buffer;
                    status = status with { Received = status.Received + 1 };
                    byte[]? output = null;
                    TelemetryFrame frame = new();
                    if (options.Mode == TelemetryMode.Fh5Relay && Fh5Packet.IsValid(packet))
                    {
                        output = packet; // Preserve every byte, including fields this application does not display.
                        frame = Fh5Packet.Dashboard(packet);
                    }
                    else if ((options.Mode == TelemetryMode.Codemasters && Decoders.TryCodemasters(packet, options.RpmScale, out frame)) ||
                             (options.Mode == TelemetryMode.ProjectCars2 && Decoders.TryProjectCars2(packet, out frame)))
                    {
                        frame = frame with { TimestampMs = unchecked((uint)clock.ElapsedMilliseconds) };
                        output = Fh5Packet.Encode(frame);
                    }
                    if (output != null)
                    {
                        await SendAsync(output);
                        status = status with { Dashboard = frame, LastReceived = DateTimeOffset.UtcNow, Stale = false };
                        lastAccepted = clock.Elapsed.TotalSeconds;
                        sampleActive = true;
                    }
                    else status = status with { Rejected = status.Rejected + 1 };
                    pending = receiver!.ReceiveAsync(token).AsTask();
                }
                if (!tick.IsCompleted) continue;
                await tick;
                if (options.Mode == TelemetryMode.Demo)
                {
                    var phase = (float)(clock.Elapsed.TotalSeconds % 8 / 8);
                    var frame = new TelemetryFrame { TimestampMs = unchecked((uint)clock.ElapsedMilliseconds),
                        Rpm = 1000 + phase * 6500, MaxRpm = 8000, IdleRpm = 900,
                        Speed = 10 + phase * 50, Gear = 1 + (int)(phase * 5), Throttle = phase, Fuel = 0.65f };
                    await SendAsync(Fh5Packet.Encode(frame));
                    status = status with { Dashboard = frame, Stale = false };
                    sampleActive = true;
                }
                else if (lastAccepted.HasValue && !status.Stale && clock.Elapsed.TotalSeconds - lastAccepted > options.StaleTimeout.TotalSeconds)
                {
                    await SendAsync(Fh5Packet.Encode(new TelemetryFrame { IsRaceOn = false }));
                    status = status with { Stale = true, Dashboard = null };
                    sampleActive = false;
                }
                tick = Task.Delay(options.Mode == TelemetryMode.Demo ? 16 : 100, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            status = status with { Error = exception.Message };
        }
        finally
        {
            if (sampleActive) await SendAsync(Fh5Packet.Encode(new TelemetryFrame { IsRaceOn = false }));
            if (pending != null)
            {
                // Observe the outstanding receive before disposing the socket.
                await cancellation.CancelAsync();
                try { await pending; } catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
            }
        }
    }

    private async Task SendAsync(byte[] bytes)
    {
        try
        {
            await sender.SendAsync(bytes, new IPEndPoint(IPAddress.Parse(options.OutputAddress), options.OutputPort));
            status = status with { Sent = status.Sent + 1 };
        }
        catch (SocketException)
        {
            // A missing Pit House listener is not an application crash. Sending is not an acknowledgement.
            status = status with { SendErrors = status.SendErrors + 1 };
        }
    }

    public async ValueTask DisposeAsync()
    {
        await cancellation.CancelAsync();
        if (loop != null) await loop;
        receiver?.Dispose(); sender.Dispose(); cancellation.Dispose();
    }
}
