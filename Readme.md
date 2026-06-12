# AuroraMic

Use your Android phone as a wireless microphone for your desktop computer.

[![Build](https://github.com/DeltaBitsSystem/AuroraMic/actions/workflows/ci.yml/badge.svg)](https://github.com/DeltaBitsSystem/AuroraMic/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-purple.svg)](https://dotnet.microsoft.com/)

## Screenshots

![Server](Screenshots/1.png)
![Client](Screenshots/2.png)

## Features

- Use any Android phone as a wireless microphone
- Streams audio over UDP on your local network
- 48kHz mono float32 audio quality
- Under 100ms latency on the same LAN
- Foreground service keeps streaming alive in the background
- Works on Windows, Linux, and Android
- No internet connection required

## Quick Start

1. Run `AuroraMic` on your desktop (Windows or Linux)
2. Select an audio output device and click **Start Server**
3. Note the IP address displayed in the app
4. Open AuroraMic on your Android phone
5. The server is **auto-discovered** -- tap its IP in the list
6. Or enter the server IP manually and tap **Start Microphone**

The phone mic audio now plays through your desktop speakers.

## Linux Virtual Audio Setup

On Linux, AuroraMic server plays audio to a PulseAudio/PipeWire sink. To route this
audio into applications like OBS, Discord, or DAWs, you need to create a virtual
microphone source.

### Setup

Run these commands in a terminal:

```
pactl load-module module-null-sink sink_name=PhoneInput sink_properties=device.description="Phone_Microphone"
pactl load-module module-remap-source master=PhoneInput.monitor source_name=PhoneMic source_properties=device.description="Phone_Mic"
```

This creates a virtual microphone called "PhoneMic" that any application can use as
an input device.

### Make It Persistent

To load these modules automatically on boot, add the two `pactl load-module` lines
to your PulseAudio configuration:

- PulseAudio: `~/.config/pulse/default.pa`
- PipeWire: `~/.config/pipewire/pipewire-pulse.conf.d/auroramic.conf`

### Verify

Check that the virtual microphone exists:

```
pactl list sources short | grep PhoneMic
```

Then select "PhoneMic" as the input device in your application.

## Building from Source

### Prerequisites

- .NET 10 SDK
- For Android builds: Android SDK, Java 17, .NET Android workload

### Server (Windows / Linux)

```bash
dotnet publish AuroraMic.Server.Desktop/AuroraMic.Server.Desktop.fsproj -c Release
```

Output: single self-contained executable in `bin/Release/net10.0/publish/`.

### Android APK

```bash
dotnet build AuroraMic.Client.Android/AuroraMic.Client.Android.fsproj -c Release
```

Output: APK in `bin/Release/net10.0-android/`.

### Tests

```bash
dotnet test AuroraMic.Tests/
```

## Configuration

The server saves settings to `settings.json` next to the executable:

```json
{
  "Port": 50006,
  "OutputDevice": "Speakers (Realtek Audio)"
}
```

Default port: `50006`. Valid range: `1024` to `65535`.

The client remembers the last server IP and port between sessions.

## Architecture

```
[Android mic]
    |
    v (48kHz mono float32)
[Android App - AuroraMic.Client]
    |
    v (UDP datagrams)
[Network - WiFi / LAN]
    |
    v (UDP receive)
[Desktop Server - AuroraMic.Server]
    |
    v (QueueDataProvider -> SoundPlayer)
[Output Device]
    |
    +---> [Speakers / Headphones]
    |
    +---> [Virtual Cable / Null Sink] ---> [OBS / Discord / DAW]

Discovery:
  Server broadcasts "AURORAMIC:<port>" on UDP 50007 every second.
  Client listens on UDP 50007, auto-discovers servers on the LAN.
```

### Protocol

| Step | Client | Server |
|---|---|---|
| 0 | Listens on UDP 50007 | Broadcasts `AURORAMIC:<audioPort>` on UDP 50007 |
| 1 | Sends "RECV" (4 bytes UDP) | Receives handshake |
| 2 | Waits for response | Sends "REDY" (4 bytes UDP) |
| 3 | Starts streaming audio | Starts playing audio |

### Projects

| Project | Target | Description |
|---|---|---|
| `AuroraMic.Server` | `net10.0` | Shared server logic (UDP receiver + audio playback) |
| `AuroraMic.Server.Desktop` | `net10.0` | Desktop entry point (Windows + Linux) |
| `AuroraMic.Client` | `net10.0` | Shared client logic (audio capture + UDP streaming) |
| `AuroraMic.Client.Android` | `net10.0-android` | Android UI + foreground service |
| `AuroraMic.Tests` | `net10.0` | Unit and integration tests |

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
