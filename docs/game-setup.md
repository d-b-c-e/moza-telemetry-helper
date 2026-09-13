# Game setup

## First prove the process gate

1. Open Pit House with no supported game running.
2. Run the helper in Process only mode, default identity `ForzaHorizon5.exe`.
3. Check whether Pit House detects FH5 and owns UDP port 20055. Port binding is evidence that the reader started; it does not prove a screen rendered telemetry.
4. Try Demo mode and observe RPM/speed/gear on the physical wheel display.
5. Stop. Verify the named helper process is gone and the synthetic values stop.

Pit House can detect the actual game too, so native reader precedence may interfere when another supported title is running. Test each game separately. A renamed sentinel contains no game code or credentials and does not start the real game.

## Games/ports that already emit FH5

The lowest-friction setup uses Process only mode and the game's existing FH5 destination. MOZA documents `127.0.0.1:20055`. If diagnostics or routing are needed, change the game's destination to `127.0.0.1:20777`, select FH5 relay, and keep the helper output at `127.0.0.1:20055`.

Relay accepts only the Horizon 324-byte layout. FM7's 311-byte Dash, 232-byte Sled, and newer Motorsport extensions are different layouts and are deliberately rejected. Packet counters expose format mismatches.

## Codemasters legacy: DiRT / GRID candidates

Enable UDP in the game's existing `hardware_settings_config.xml` while the game is closed. Back it up first. The path and supported `extradata` version vary by title. A typical legacy entry under the existing `motion_platform` section is:

```xml
<udp enabled="true" extradata="3" ip="127.0.0.1" port="20777" delay="1" />
```

Edit the existing entry; do not replace the entire configuration. Select Codemasters mode and the matching input port. `delay` scheduling may vary by game. The helper does not edit this file.

This version **requires exactly 264 bytes (66 little-endian floats)** and valid RPM limits. DiRT Rally's reference parser documents current/max/idle engine values as RPM divided by 10, hence the default multiplier 10. Other titles must be checked against the tachometer; change the multiplier if the source uses plain RPM. There is no automatic format guessing.

DiRT 3, DiRT 4, original GRID, GRID 2, GRID Autosport, and GRID (2019) must not be treated as interchangeable. Some earlier versions emit a shorter legacy packet or ignore `extradata=3`. If Received rises while Sent stays zero, check packet length and exact title before assuming wheel incompatibility. Short-packet adapters are follow-up work.

DiRT 4 has native MOZA support. Compare that route before relying on translation. Modern F1 protocols and EA WRC's configurable UDP protocol need separate adapters.

## Project CARS 2

In the game's system settings, enable UDP frequency and choose the **Project CARS 2** UDP protocol. The initial helper mode receives the SMS car-physics v2 packet (556 bytes), normally broadcast on port **5606**. The UI selects **0.0.0.0:5606** for this mode so LAN/broadcast packets can arrive.

Select Project CARS 2 mode, leave FH5 output at the Pit House FH5 port, and check the moving values. Do not choose the Project CARS 1 protocol or shared-memory-only output for this adapter. Broadcast/firewall behavior varies by machine; limit any firewall rule to the networks you use.

The current converter handles the viewed participant's speed, RPM/max RPM, pedals, steering, gear, fuel fraction, odometer and handbrake. It ignores names/timings/session packets, which count under Ignored / invalid. Spectator/replay samples are not distinguished. Full session-state and lap-time reconstruction are on the roadmap.

MOZA lists Project CARS 2 as natively supported; native detection can also take precedence over the fake FH5 process. That is a separate compatibility test from decoding UDP.

## Diagnosing a blank screen

- Helper process absent: inspect the application's log or headless report.
- Process exists, FH5 reader absent: test Pit House version/detection and other running games.
- Received = 0: verify source endpoint, game telemetry setting, broadcast binding and firewall.
- Ignored / invalid increases: verify protocol, packet size and version. PCARS2 non-physics packets are expected here.
- Sent increases, screen blank: UDP send has no acknowledgement. Check destination port, Pit House active reader, wheel/dashboard selection, firmware and live-screen behavior.
- Values stop after two seconds: no valid samples are arriving; the helper sends one race-off packet instead of repeating the last race sample.

Stop before launching real FH5; running the stub alongside its namesake is intentionally discouraged. To bridge a real recognized game without a stub, uncheck Run process helper.
