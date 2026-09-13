# Protocol references and boundaries

Investigated 2026-09-12. Code is an independent implementation of wire layouts; no third-party implementations or local Pit House binaries are vendored.

## Primary / implementation references

- [MOZA FH4 / FH5 telemetry configuration](https://support.mozaracing.com/en/support/solutions/articles/70000625637-forza-horizon-4-5-telemetry-configuration): FH5 loopback port 20055; FH4 uses 20044.
- [MOZA game compatibility](https://support.mozaracing.com/en/support/solutions/articles/70000629729-game-support-list): native PCARS2 and DiRT 4 support.
- [MOZA dashboard telemetry fields](https://support.mozaracing.com/en/support/solutions/articles/70000627978-digital-dash-telemetry-support): available dashboard properties vary by game.
- [Forza telemetry decoder](https://github.com/0x20F/forza-telemetry/tree/master/src/decoder): maintained decoder of the Horizon layout, 12-byte gap and packet sizes. Community implementation, not an official FH5 protocol guarantee.
- [DiRT Rally logger source](https://github.com/ErlerPhilipp/dr2_logger/blob/master/source/dirt_rally/udp_data.py): observed 66-float layout, distance at byte 8 versus progress at 12, RPM scale 10.
- [SMS UDP definitions](https://github.com/saildeep/pcars2-udp/blob/master/SMS_UDP_Definitions.hpp): distribution of the Project CARS 2 packet header, physics v2 offsets and sizes. Comments in later structs contain inconsistencies; initial support deliberately uses the stable leading physics fields and handbrake offset.

## FH5 output

324 bytes, little endian. Sled at 0–231, Horizon gap at 232–243, dashboard begins at 244. Core mappings: race-active i32 at 0, timestamp u32 at 4, max/idle/current RPM f32 at 8/12/16, speed m/s at 256, fuel fraction at 288, distance meters at 292, last/current lap and race seconds at 300/304/308, lap u16 at 312, position u8 at 314, pedals u8 at 315–318, gear u8 at 319, steering i8 at 320. Final byte remains zero for converted packets. Normalized gear −1/0/1+ becomes Forza 0/11/1+; neutral display behavior needs physical verification.

Unknown source fields remain zero, including car identity, temperatures and most Sled channels. These conversions are intended for basic dashboard values, not motion systems or full vehicle physics. FH5 relay preserves all incoming bytes after validating packet length, race flag and finite primary dashboard numbers.

## Codemasters input

Exactly 264 bytes. Current/max/idle engine floats at 148/252/256 multiplied by the user setting. Speed f32 at 28, throttle/steering/brake/clutch at 116/120/124/128, gear f32 at 132 (0 neutral, 10 reverse), distance f32 at 8, fuel/capacity at 180/184, race/current/last lap times at 0/4/248. Every float must be finite; invalid RPM limits or impossible gears are rejected. Valid samples imply active telemetry; explicit paused/menu interpretation is not implemented.

## PCARS2 input

Exactly 556 bytes, category 0 at byte 10, version 2 at byte 11. Speed f32 at 36, RPM/max u16 at 40/42, steering i8 at 44, gear lower nibble at 45 (0 neutral, 15 reverse; high nibble is gear count). Fuel f32 at 32, odometer km at 48 converted to meters, pedals at 29/30/31, handbrake at 370. Negative viewed-participant indices are rejected. Other categories/versions are ignored; full session state is not inferred from them.

## Process identity

Windows reports the sentinel under its actual selected executable filename. It is copied to a unique per-user session directory; a .NET single-file publish carries its own application payload so renaming does not require changing an assembly name. The parent retains the exact child Process object and uses private stop/ready events. The sentinel retains the original parent's process handle and checks its creation time to guard against PID reuse. It exits if the parent dies.

The installed Pit House 1.4.0.30 executable contains `ForzaHorizon5.exe`. That is evidence for the default, not proof that filename is its only detection condition. Port activation and physical display behavior are separate verification steps.
