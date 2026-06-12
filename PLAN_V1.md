# AuroraMic v1.0 -- Master Plan

> Source of truth for AuroraMic v1.0. Any AI or human implementer follows this document.
> Last verified: 2026-06-11. .NET SDK 10.0.300, Avalonia 11.3.14, SoundFlow 1.4.1.
> Discovery: UDP broadcast on port 50007, marker `AURORAMIC:<audioPort>`, 1s interval.

---

## 1. Product Vision

AuroraMic turns an Android phone into a wireless microphone for a desktop computer over the local network. The phone captures audio, streams it via UDP, and the desktop server plays it through any output device -- speakers, headphones, or a virtual audio cable for routing into OBS, Discord, DAWs, and other applications.

### 1.1 Target Users

- Content creators who need a quick wireless mic without hardware
- Developers doing presentations who want mobility
- Anyone who needs a temporary microphone without buying equipment

### 1.2 v1.0 Success Criteria

| Criterion | Target |
|---|---|
| Latency (mic to speaker) | Under 100ms on same LAN |
| Connection time | Under 3 seconds from tap to streaming |
| Crash rate | Under 0.1% of sessions |
| Platform support | Windows 10+, Linux (PulseAudio/PipeWire), Android 10+ |
| Audio quality | 48kHz mono float32, no audible artifacts on stable WiFi |
| Setup complexity | User reads 5-step guide, no terminal required for basic use |

---

## 2. Current State Assessment

### 2.1 What Exists (and Works)

- 4 F# source files, ~970 lines total
- Server: UDP receiver + audio playback + settings + UI (444-line file, well-organized with modules)
- Client: UDP sender + audio capture + UI (316-line file, clean modules)
- Android: Foreground service, permissions, splash screen
- Desktop: Single-file publish with ReadyToRun
- Audio pipeline: 48kHz mono float32 over UDP, works reliably on LAN
- UI: Dark theme, Fluent design, functional and clean

### 2.2 What Needs Fixing for v1.0

| ID | Issue | Severity | v1.0 Action |
|---|---|---|---|
| B-01 | No LICENSE file | Blocker | Add MIT LICENSE |
| B-02 | Android package name is placeholder `com.CompanyName.AvaloniaTest` | Blocker | Rename to `com.auroramic.app` |
| B-03 | Silent exception swallowing in 6+ locations | High | Add logging to catch blocks |
| B-04 | No resource cleanup on app exit | High | Add cleanup handlers |
| B-05 | 4 unused NuGet packages | Medium | Remove them |
| B-06 | Avalonia version mismatch (11.3.10 vs 11.3.11) | Medium | Align to 11.3.14 |
| B-07 | No Linux audio routing docs | Medium | Add to README |
| B-08 | No test project | Medium | Create with basic smoke tests |
| B-09 | No CI pipeline | Medium | Add build + test workflow |
| B-10 | Client does not persist server IP | Low | Add SharedPreferences |
| B-11 | No loading state during connection | Low | Add spinner |
| B-12 | Error messages could be clearer | Low | Improve copy |

### 2.3 Explicitly Deferred to v1.1+

These items are NOT in scope for v1.0. The software works without them.

| Item | Reason to Defer |
|---|---|
| Opus encoding | 192 KB/s on LAN is <1% of WiFi capacity. Not a real problem. |
| Protocol header (sequence numbers, timestamps) | Wire protocol break for diagnostics only. High risk. |
| AUTH/PIN authentication | LAN-only tool. PIN in plaintext adds UX friction for minimal security. |
| Project restructuring (split App.fs) | Files are already well-organized with modules. ~450 lines is fine. |
| FuncUI 1.5.2 -> 1.6.0 upgrade | API break risk across entire UI. Not worth it for v1.0. |
| Linux virtual mic in-app automation | README instructions are sufficient for v1.0. |
| Responsive layout | 480x800 is fine for a utility app. |
| Accessibility (ARIA, keyboard nav) | Can be added incrementally in v1.1. |
| i18n | English only is fine for v1.0. |
| Audio level meter | Nice-to-have, not essential. |
| Packet loss display | Diagnostic, not essential. |
| Keyboard shortcuts | Mouse works fine for a utility. |

---

## 3. Architecture Decision Records

### ADR-01: Keep F# + Avalonia.FuncUI

**Status:** Accepted

**Context:** The project uses F# with Avalonia.FuncUI (Elmish/MVU pattern). All UI is written in F# code, no AXAML.

**Decision:** Retain this stack. F# type safety and functional patterns are well-suited for audio/streaming code. FuncUI provides clean declarative UI.

**Consequences:**
- All new code must be F#
- UI follows Component + ctx.useState pattern
- No mixing with C# UI code

### ADR-02: Keep UDP Transport As-Is

**Status:** Accepted

**Context:** Real-time audio requires low latency. UDP works well. The current RECV/REDY handshake is simple and functional.

**Decision:** Keep the current UDP protocol unchanged for v1.0. No header changes, no sequence numbers. The protocol works.

**Consequences:**
- No wire protocol break
- No risk of breaking existing connections
- Sequence numbers and timestamps deferred to v1.1 when concrete need arises

### ADR-03: Keep Audio Format As-Is

**Status:** Accepted

**Context:** Current format is 48kHz mono float32 raw PCM. Bandwidth is ~192 KB/s (1.5 Mbps). On WiFi 5+, this is less than 1% of capacity.

**Decision:** Keep 48kHz mono float32 raw PCM. No Opus encoding for v1.0.

**Consequences:**
- No encoding/decoding latency
- No Concentus dependency in the hot path
- No frame-size alignment issues
- Bandwidth is not a problem on modern LAN

### ADR-04: Linux Virtual Mic via README Only

**Status:** Accepted

**Context:** On Linux, the server plays audio to a PulseAudio/PipeWire sink. Apps like OBS and Discord capture from sources. A null-sink + remap-source is needed.

**Decision:** Document the manual setup in README. No in-app automation for v1.0.

**Commands required (documented, not automated):**
```bash
pactl load-module module-null-sink sink_name=PhoneInput sink_properties=device.description="Phone_Microphone"
pactl load-module module-remap-source master=PhoneInput.monitor source_name=PhoneMic source_properties=device.description="Phone_Mic"
```

**Consequences:**
- Works on both PulseAudio and PipeWire (identical syntax)
- No process execution code to maintain
- In-app automation deferred to v1.1

### ADR-05: No Authentication for v1.0

**Status:** Accepted

**Context:** Any device on the LAN can currently connect and stream audio to the server.

**Decision:** No authentication for v1.0. The threat model (someone on your WiFi streams audio to your speakers) is a minor annoyance, not a security issue. Adding a PIN adds UX friction (user must type it on every session).

**Consequences:**
- No handshake protocol change
- No PIN management UI
- Optional PIN added in v1.1 if users request it

### ADR-06: Keep Current Project Structure

**Status:** Accepted

**Context:** Both App.fs files are 444 and 316 lines respectively, but are well-organized with named modules inside.

**Decision:** Keep the current file structure for v1.0. The modules inside each file are clearly named and separated. Splitting into multiple files introduces F# compilation order risks.

**Consequences:**
- No file move refactoring
- No compilation order debugging
- Can be done incrementally in v1.1

### ADR-07: Conservative Dependency Updates

**Status:** Accepted

**Context:** Some packages are outdated or mismatched.

**Decision:** Update only patch versions. No minor/major upgrades that could break APIs.

| Package | Current | Target | Change Type |
|---|---|---|---|
| Avalonia (all) | 11.3.10/11.3.11 | 11.3.14 | Patch (safe) |
| Avalonia.FuncUI | 1.5.2 | 1.5.2 | No change (skip 1.6.0) |
| SoundFlow | 1.4.0 | 1.4.1 | Patch (safe) |
| Concentus | 2.2.2 | REMOVE | Unused |
| Avalonia.FuncUI.Elmish | 1.5.2 | REMOVE | Unused |
| Avalonia.ReactiveUI | 11.3.8 | REMOVE | Unused |
| SoundFlow.Extensions.WebRtc.Apm | 1.4.0 | REMOVE | Unused |

**Consequences:**
- No API breakage from version bumps
- FuncUI stays on 1.5.2 (proven working)
- Smaller build output with removed packages

---

## 4. Technical Specification

### 4.1 Protocol (Unchanged)

The v1.0 protocol remains exactly as implemented:

```
Client                              Server
  |                                    |
  |--- "RECV" (4 bytes UDP) --------->|
  |                                    |
  |<---- "REDY" (4 bytes UDP) --------|
  |                                    |
  |--- [audio float32 bytes] -------->|
  |--- [audio float32 bytes] -------->|
  |           ...                     |
```

No header, no sequence numbers, no AUTH. This is the v1.0 protocol.

### 4.8 UDP Discovery

**Discovery broadcast:** Server broadcasts `AURORAMIC:<audioPort>` on UDP port 50007 every second.
**Client listener:** Client listens on UDP port 50007 for broadcasts, builds a list of discovered servers.
**Tap to connect:** Discovered servers appear in the UI; tapping one fills the IP/port fields.

```
Server                                    Client
  |                                          |
  |--- "AURORAMIC:50006" (UDP 50007) ------>|  (broadcast every 1s)
  |                                          |
  |--- "AURORAMIC:50006" (UDP 50007) ------>|  (if still running)
  |           ...                            |
```

**Server module:** `Discovery` in `Server/App.fs` -- broadcasts until `stop()` is called.
**Client module:** `DiscoveryListener` in `Client/App.fs` -- listens, maintains server list with dedup.
**Integration:** Server starts discovery in `MainView`. Client starts listener in `App.Initialize`, stops in `ProcessExit` and `AndroidMic.stop()`.

### 4.2 Audio Pipeline (Unchanged)

**Server:**
```
UDP receive -> MemoryMarshal.Cast<byte, float32> -> QueueDataProvider(4800, Drop) -> SoundPlayer -> Output Device
```

**Client:**
```
Microphone -> MiniAudio capture (48kHz mono float32) -> MemoryMarshal.Cast<float32, byte> -> UDP send
```

No Opus, no pre-allocated buffers. The current implementation works.

### 4.3 Error Handling

**Rule:** Replace `with _ -> ()` and `with _ -> defaults` with proper logging.

| Location | Current | v1.0 Change |
|---|---|---|
| `Server/App.fs:40` | `with _ -> defaults` | `with ex -> printfn "Config load error: %s" ex.Message; defaults` |
| `Server/App.fs:44` | `with _ -> ()` | `with ex -> printfn "Config save error: %s" ex.Message` |
| `Server/App.fs:62` | `with _ -> ()` | `with ex -> printfn "Dispose error: %s" ex.Message` |
| `Server/App.fs:129` | `with _ -> ()` | `with ex -> printfn "Task wait error: %s" ex.Message` |
| `Server/App.fs:130` | `with _ -> ()` | `with ex -> printfn "UDP close error: %s" ex.Message` |
| `Server/App.fs:191` | `with _ -> [||]` | `with ex -> printfn "Audio device list error: %s" ex.Message; [||]` |
| `Client/App.fs:45` | `with _ -> false` | `with ex -> printfn "Server check error: %s" ex.Message; false` |
| `Client/App.fs:99` | `with :? SocketException -> ()` | `with :? SocketException as ex -> printfn "UDP send error: %s" ex.Message` |

**Why printfn instead of Microsoft.Extensions.Logging:** Adding a logging framework changes the dependency graph and requires setup in every project. For v1.0, `printfn` is sufficient and zero-risk. Structured logging is a v1.1 improvement.

### 4.4 Resource Cleanup

**Desktop (Server):**
- Register `AppDomain.CurrentDomain.ProcessExit` handler in `Program.fs`
- Handler calls `NetworkServer.stop()` and `AudioEngine.stop()`

**Android (Client):**
- Add `OnDestroy` override to `AudioStreamingService`
- Handler calls `AndroidMic.stop()` if running

**Client App.fs:**
- Register cleanup in `App.Initialize` via `AppDomain.CurrentDomain.ProcessExit`

### 4.5 Settings Schema (Unchanged)

```json
{
  "Port": 50006,
  "OutputDevice": ""
}
```

No `EnableOpus` field. No protocol changes.

### 4.6 Client IP Persistence

**New behavior:** Client saves the last-used server IP and port to a local config file.

**File location:** `{AppData}/AuroraMic/client-settings.json` (platform-specific app data directory)

**Schema:**
```json
{
  "LastServerIp": "192.168.1.100",
  "LastServerPort": 50006
}
```

**Behavior:**
- On startup, load and populate the IP/port fields
- On successful connect, save the current IP/port
- If file is missing or corrupt, use empty defaults

### 4.7 Loading State

**New behavior:** When the client is connecting (handshake in progress), show a visual indicator.

**Implementation:**
- Add `isConnecting` state variable to client UI
- Set to `true` before `AndroidMic.start()`
- Set to `false` after start completes (success or failure)
- When `isConnecting` is true: disable the Start button, show "Connecting..." text
- This is a UI-only change; the handshake timeout remains 2 seconds

---

## 5. Test Strategy (100% Coverage Required)

> "It works" is not the same as "it is verified to work." Every module must have tests
> that prove its behavior. If a test cannot be automated (e.g., requires hardware), it
> must be documented as a manual test with explicit steps and expected results.

### 5.1 Test Framework

- **xUnit** (standard for .NET, works with F#)
- **Coverlet** for code coverage measurement
- No FsCheck for v1.0 (property-based testing is a v1.1 addition)

### 5.2 Coverage Requirement

**100% line coverage** of all business logic modules. This is measured with Coverlet and enforced in CI.

**What counts as "business logic":**
- Config load/save
- Network handshake validation
- Audio format constants
- IP address enumeration
- Error handling paths
- State management

**What does NOT count (UI layer):**
- FuncUI `Component` view functions (require Avalonia runtime)
- Android Activity/Service lifecycle (require Android emulator)

### 5.3 Test Project Structure

```
AuroraMic.Tests/
  AuroraMic.Tests.fsproj
  ConfigTests.fs           -- Server config load/save
  ClientConfigTests.fs     -- Client config load/save (IP persistence)
  NetworkInfoTests.fs      -- IP address enumeration
  HandshakeTests.fs        -- Server-side handshake validation
  ClientHandshakeTests.fs  -- Client-side handshake logic
  AudioFormatTests.fs      -- Audio format constants and queue config
  ErrorHandlerTests.fs     -- Verify catch blocks log (not swallow)
  IntegrationTests.fs      -- Server lifecycle integration tests
```

### 5.4 Test Cases by Module

#### Config Tests (Server) -- `ConfigTests.fs`

| # | Test | What It Verifies | Type |
|---|---|---|---|
| C-01 | `load_validJson_returnsCorrectValues` | Parses `{"Port":50006,"OutputDevice":"Headphones"}` correctly | Unit |
| C-02 | `load_missingFile_returnsDefaults` | Returns `{Port=50006; OutputDevice=""}` when file does not exist | Unit |
| C-03 | `load_emptyFile_returnsDefaults` | Returns defaults on empty file | Unit |
| C-04 | `load_corruptJson_returnsDefaults` | Returns defaults on malformed JSON | Unit |
| C-05 | `load_partialJson_fillsMissingFields` | Returns defaults for missing fields | Unit |
| C-06 | `load_portOutOfRange_usesValue` | Loads port as-is (validation happens at runtime) | Unit |
| C-07 | `save_validSettings_createsFile` | File exists after save | Unit |
| C-08 | `save_roundTrip_matchesOriginal` | Save then load returns identical values | Unit |
| C-09 | `save_invalidPath_returnsError` | Handles write failure gracefully | Unit |
| C-10 | `defaults_portIs50006` | Default port is 50006 | Unit |
| C-11 | `defaults_outputDeviceIsEmpty` | Default output device is empty string | Unit |

#### Config Tests (Client) -- `ClientConfigTests.fs`

| # | Test | What It Verifies | Type |
|---|---|---|---|
| CC-01 | `load_validJson_returnsIpAndPort` | Parses `{"LastServerIp":"192.168.1.5","LastServerPort":50006}` | Unit |
| CC-02 | `load_missingFile_returnsEmpty` | Returns empty IP, default port | Unit |
| CC-03 | `load_corruptFile_returnsEmpty` | Returns empty IP, default port | Unit |
| CC-04 | `save_roundTrip_matchesOriginal` | Save then load returns identical values | Unit |
| CC-05 | `defaults_lastServerIpIsEmpty` | Default IP is empty string | Unit |
| CC-06 | `defaults_lastServerPortIs50006` | Default port is 50006 | Unit |

#### Network Info Tests -- `NetworkInfoTests.fs`

| # | Test | What It Verifies | Type |
|---|---|---|---|
| NI-01 | `localIPv4_returnsNonEmpty` | Returns at least one IP address on a connected machine | Unit |
| NI-02 | `localIPv4_allAreValidIpFormat` | Every returned string parses as a valid IPv4 address | Unit |
| NI-03 | `localIPv4_noLoopback` | Does not include 127.0.0.1 | Unit |
| NI-04 | `localIPv4_noDuplicates` | No duplicate addresses in the result | Unit |

#### Handshake Tests (Server) -- `HandshakeTests.fs`

These tests verify the server-side handshake logic. The `handleHandshake` function receives a byte array and returns a boolean.

| # | Test | What It Verifies | Type |
|---|---|---|---|
| H-01 | `handleHandshake_validRecv_returnsTrue` | `System.Text.Encoding.ASCII.GetBytes("RECV")` returns `true` | Unit |
| H-02 | `handleHandshake_wrongBytes_returnsFalse` | `System.Text.Encoding.ASCII.GetBytes("ABCD")` returns `false` | Unit |
| H-03 | `handleHandshake_tooShort_returnsFalse` | 3-byte array returns `false` | Unit |
| H-04 | `handleHandshake_tooLong_returnsFalse` | 5-byte array returns `false` | Unit |
| H-05 | `handleHandshake_emptyArray_returnsFalse` | Empty array returns `false` | Unit |
| H-06 | `handleHandshake_lowercaseRecv_returnsFalse` | `"recv"` (lowercase) returns `false` | Unit |

**Note:** These tests require extracting the handshake validation logic into a testable function. If the logic is inline in the receive loop, extract it first:

```fsharp
// In Server/App.fs, inside NetworkServer module:
let handleHandshake (data: byte[]) =
    data.Length = 4
    && System.Text.Encoding.ASCII.GetString(data) = "RECV"
```

#### Client Handshake Tests -- `ClientHandshakeTests.fs`

These tests verify the client-side handshake logic. The `checkServerReady` function sends "RECV" and waits for "REDY".

| # | Test | What It Verifies | Type |
|---|---|---|---|
| CH-01 | `buildRecvPacket_returnsFourBytes` | The packet sent is exactly `System.Text.Encoding.ASCII.GetBytes("RECV")` | Unit |
| CH-02 | `validateRedyResponse_valid_returnsTrue` | 4-byte `"REDY"` response returns `true` | Unit |
| CH-03 | `validateRedyResponse_wrongBytes_returnsFalse` | 4-byte non-REDY response (e.g. "ABCD") returns `false` | Unit |
| CH-04 | `validateRedyResponse_tooShort_returnsFalse` | Short response returns `false` | Unit |

**Integration test (requires two UDP sockets):**

| # | Test | What It Verifies | Type |
|---|---|---|---|
| CH-05 | `handshake_integration_succeeds` | Start UDP server on localhost, send RECV, receive REDY | Integration |
| CH-06 | `handshake_integration_wrongPort_fails` | Send RECV to wrong port, timeout returns false | Integration |

#### Audio Format Tests -- `AudioFormatTests.fs`

| # | Test | What It Verifies | Type |
|---|---|---|---|
| AF-01 | `format_is48kHzMonoFloat32` | `SampleRate = 48000`, `Channels = 1`, `Format = SampleFormat.F32` | Unit |
| AF-02 | `queueCapacity_is4800` | Queue capacity is 4800 samples | Unit |
| AF-03 | `queueBehavior_isDrop` | Queue full behavior is `QueueFullBehavior.Drop` | Unit |
| AF-04 | `queueBufferRepresents100ms` | 4800 samples / 48000 Hz = 0.1 seconds | Unit |
| AF-05 | `bytesPerSample_is4` | `sizeof<float32>` = 4 bytes | Unit |

#### Discovery Tests -- `DiscoveryTests.fs`

| # | Test | What It Verifies | Type |
|---|---|---|---|
| D-01 | `discoveryMarker_isCorrectFormat` | `"AURORAMIC"` marker is correct ASCII string | Unit |
| D-02 | `discoveryBroadcastPort_is50007` | Broadcast port is 50007 | Unit |
| D-03 | `discoveryMessage_buildsCorrectly` | `AURORAMIC:<port>` message format is correct | Unit |
| D-04 | `discoveryMessage_parsesCorrectly` | Parsing `"AURORAMIC:50006"` extracts port 50006 | Unit |
| D-05 | `discoveryMessage_invalidFormat_ignores` | Malformed message (no colon, bad port) is ignored | Unit |
| D-06 | `discoveryListener_startStop_noException` | Start then stop does not throw | Unit |
| D-07 | `discoveryListener_deduplicates` | Same server IP is not added twice | Unit |

#### Error Handler Tests -- `ErrorHandlerTests.fs`

These tests verify that catch blocks log instead of swallowing. Since we use `printfn`, we can capture stdout.

| # | Test | What It Verifies | Type |
|---|---|---|---|
| EH-01 | `configLoad_error_logsMessage` | Corrupt config file triggers a printfn (capture stdout) | Unit |
| EH-02 | `configSave_error_logsMessage` | Write to invalid path triggers a printfn | Unit |

**Note:** Capturing `printfn` output in F# tests requires redirecting `Console.Out`. If this is not feasible, these tests verify the behavior manually:
- Corrupt the settings.json file
- Start the server
- Verify it prints an error message and continues with defaults

#### Integration Test: Full Connection -- `IntegrationTests.fs`

| # | Test | What It Verifies | Type |
|---|---|---|---|
| IT-01 | `serverStarts_listensOnPort` | Server binds to port and `NetworkServer.port()` returns correct value | Integration |
| IT-02 | `serverStops_releasesPort` | After stop, port is available again | Integration |
| IT-03 | `serverStopStart_cycle_works` | Start, stop, start again succeeds | Integration |

### 5.5 Manual Test Checklist (Non-Automatable)

These tests require hardware or UI interaction and cannot be automated in v1.0. They must be executed and checked off before release.

| # | Test | Steps | Expected Result |
|---|---|---|---|
| M-01 | Server starts on Linux | Build, run on Linux with PulseAudio | Server window opens, no crash |
| M-02 | Server starts on Windows | Build, run on Windows | Server window opens, no crash |
| M-03 | Android app installs | Build APK, install on Android 10+ device | App installs and launches |
| M-04 | Android permission prompt | Launch app on fresh install | RECORD_AUDIO permission dialog appears |
| M-05 | Server shows IP addresses | Start server on LAN | All local IPs displayed in UI |
| M-06 | Client connects to server | Enter server IP, tap Start | Status changes to "Streaming to..." |
| M-07 | Audio plays through speakers | Connect client, speak into phone | Audio heard from desktop speakers |
| M-08 | Audio plays through virtual mic | Set up null-sink, select as output, connect client | PhoneMic source available in OBS |
| M-09 | Server stop releases resources | Start server, stop server, check port | Port is released, no zombie process |
| M-10 | Client stop releases mic | Start streaming, stop streaming | Android mic indicator disappears |
| M-11 | Background streaming | Start streaming, switch to another app on Android | Audio continues (foreground service) |
| M-12 | Port below 1024 validation | Set port below 1024 via settings.json (UI enforces min 1024) | Error: "Port must be between 1024-65535" |
| M-13 | Port in use shows error | Start two servers on same port | Second server shows "Port already in use" |
| M-14 | Wrong IP shows error | Enter non-existent IP, tap Start | Error message displayed indicating server is unreachable |
| M-15 | Settings persist | Start server with custom port, close, reopen | Port is remembered |
| M-16 | Client IP persists | Connect once, close app, reopen | Previous IP is prefilled |

### 5.6 Coverage Enforcement

Add to `.github/workflows/ci.yml`:

```yaml
- name: Run tests with coverage
  run: dotnet test AuroraMic.Tests/ --collect:"XPlat Code Coverage" --results-directory ./coverage

- name: Check coverage threshold
  run: |
    REPORT=$(find ./coverage -name "coverage.cobertura.xml" | head -1)
    dotnet tool install -g dotnet-reportgenerator-globaltool 2>/dev/null || true
    reportgenerator "-reports:$REPORT" "-targetdir:./report" "-reporttype:TextSummary" 2>/dev/null
    COVERAGE=$(grep "Line Coverage" ./report/Summary.txt | awk '{print $3}' | tr -d '%')
    echo "Line coverage: ${COVERAGE}%"
    if [ "$(echo "$COVERAGE < 100" | bc)" -eq 1 ]; then
      echo "Coverage ${COVERAGE}% is below 100% threshold"
      exit 1
    fi
```

### 5.7 TDD Workflow

For each new test file:
1. Write the test file first
2. Run `dotnet test` -- verify tests fail (RED)
3. If testing existing code: verify the production code passes (it should, since it works)
4. If testing new code: implement the minimum code to pass (GREEN)
5. If any test fails: fix the production code, NOT the test
6. Commit with message: `[test] module: description`

### 5.8 Coverage Targets

| Module | Target | Measurement |
|---|---|---|
| Config (Server) | 100% line coverage | Coverlet |
| Config (Client) | 100% line coverage | Coverlet |
| NetworkInfo | 100% line coverage | Coverlet |
| Handshake validation | 100% line coverage | Coverlet |
| Audio format constants | 100% line coverage | Coverlet |
| Error handlers | 100% line coverage | Coverlet |
| UI components | Manual test only | Checklist M-01 to M-16 |
| Android lifecycle | Manual test only | Checklist M-03, M-04, M-10, M-11 |
| **Overall** | **100% of automatable code** | **Coverlet + CI gate** |

---

## 6. Implementation Phases

### Phase 0: Foundation (Day 1)

**Goal:** Project hygiene, no behavior changes. All tasks are low-risk.

| # | Task | Files | Risk | Acceptance Criteria |
|---|---|---|---|---|
| 0.1 | Add MIT LICENSE | `LICENSE` | None | File exists with correct copyright |
| 0.2 | Rename Android package | `AuroraMic.Client.Android.fsproj`, `Activities.fs` | Low | Package is `com.auroramic.app`, build succeeds |
| 0.3 | Align Avalonia to 11.3.14 | All `.fsproj` | Low | All Avalonia packages reference 11.3.14 |
| 0.4 | Update SoundFlow to 1.4.1 | `AuroraMic.Server.fsproj`, `AuroraMic.Client.fsproj` | Low | Build succeeds |
| 0.5 | Remove unused packages | `AuroraMic.Server.fsproj`, `AuroraMic.Client.fsproj` | Low | Build succeeds, smaller output |
| 0.6 | Remove dead code | `AuroraMic.Server/App.fs` | Low | WaveformVisualizer removed, AudioState type updated, no unused imports |
| 0.7 | Create test project with Coverlet | `AuroraMic.Tests/` | None | `dotnet build` succeeds, coverage tool configured |
| 0.8 | Add .editorconfig | `.editorconfig` | None | File exists with F# rules |

**Verification:**
```bash
dotnet build AuroraMic.slnx -c Release
dotnet test AuroraMic.Tests/
```

### Phase 1: Error Handling & Cleanup (Day 1-2)

**Goal:** Replace silent failures with logging. Add resource cleanup.

| # | Task | Files | Risk | Acceptance Criteria |
|---|---|---|---|---|
| 1.1 | Replace `with _ ->` with logging | `AuroraMic.Server/App.fs`, `AuroraMic.Client/App.fs` | Low | Every catch block logs the exception |
| 1.2 | Add ProcessExit cleanup (desktop) | `AuroraMic.Server.Desktop/Program.fs` | Low | Server stops cleanly on process exit |
| 1.3 | Add OnDestroy (Android service) | `AuroraMic.Client.Android/Activities.fs` | Low | Service releases mic on destroy |
| 1.4 | Add ProcessExit cleanup (client) | `AuroraMic.Client/App.fs` | Low | Client stops cleanly on process exit |

**Verification:**
```bash
dotnet build AuroraMic.slnx -c Release
# Manual: start server, kill process, verify no resource leak
# Manual: start client, force close, verify mic is released
```

### Phase 2: UX Improvements (Day 2-3)

**Goal:** Small UX fixes that make the app feel polished.

| # | Task | Files | Risk | Acceptance Criteria |
|---|---|---|---|---|
| 2.1 | Persist server IP in client | `AuroraMic.Client/App.fs` | Low | IP/port prefilled on next launch |
| 2.2 | Add loading state during connect | `AuroraMic.Client/App.fs` | Low | Button disabled + "Connecting..." during handshake |
| 2.3 | Improve error messages | `AuroraMic.Client/App.fs`, `AuroraMic.Server/App.fs` | Low | Clear, actionable messages |

**Verification:**
```bash
dotnet build AuroraMic.slnx -c Release
# Manual: connect once, close, reopen -- IP should be prefilled
# Manual: click Start -- button should show "Connecting..." briefly
# Manual: enter wrong IP -- error message should be clear
```

### Phase 3: README & Documentation (Day 3)

**Goal:** Complete README with Linux audio setup instructions.

| # | Task | Files | Risk | Acceptance Criteria |
|---|---|---|---|---|
| 3.1 | Rewrite README | `Readme.md` | None | All sections present per spec below |

**Verification:**
```bash
# Read the README, verify all sections are present
# Follow the Quick Start guide on a fresh machine
# Follow the Linux setup on a Linux machine
```

### Phase 4: CI & Release (Day 3-4)

**Goal:** Automated build pipeline and v1.0.0 release.

| # | Task | Files | Risk | Acceptance Criteria |
|---|---|---|---|---|
| 4.1 | Add CI workflow | `.github/workflows/ci.yml` | None | Build + test on push |
| 4.2 | Tag v1.0.0 | Git | None | Tag exists |
| 4.3 | Create GitHub Release | GitHub | None | Release with desktop exe + Android APK |

**Verification:**
```bash
git tag v1.0.0
git push origin v1.0.0
# Verify GitHub Actions runs successfully
# Verify release artifacts are downloadable
```

---

## 7. Quality Gates

Every phase must pass these gates before proceeding:

| Gate | Command | Pass Criteria |
|---|---|---|
| Build | `dotnet build AuroraMic.slnx -c Release` | 0 errors |
| Test | `dotnet test AuroraMic.Tests/` | All tests pass |
| Coverage | `dotnet test AuroraMic.Tests/ --collect:"XPlat Code Coverage"` | 100% line coverage of automatable code |
| No secrets | Review diff before commit | No keys, tokens, passwords |

**Coverage gate is non-negotiable.** If coverage drops below 100%, the CI pipeline fails. No exceptions.

---

## 8. README Specification

The README must contain these sections in this order:

1. **Header** -- Project name, one-line description, badges (build, license, version)
2. **Screenshots** -- Server + Client UI screenshots
3. **Features** -- Bullet list of v1.0 features
4. **Quick Start** -- 5-step guide for basic use
5. **Linux Virtual Audio Setup** -- The pactl commands with explanation
6. **Building from Source** -- Prerequisites, build commands
7. **Configuration** -- settings.json schema
8. **Architecture** -- ASCII diagram of data flow
9. **License** -- MIT badge and link

### Linux Audio Setup Section (verbatim for README)

```markdown
## Linux Virtual Audio Setup

On Linux, AuroraMic server plays audio to a PulseAudio/PipeWire sink. To route this
audio into applications like OBS, Discord, or DAWs, you need to create a virtual
microphone source.

### Setup

Run these commands in a terminal:

    pactl load-module module-null-sink sink_name=PhoneInput sink_properties=device.description="Phone_Microphone"
    pactl load-module module-remap-source master=PhoneInput.monitor source_name=PhoneMic source_properties=device.description="Phone_Mic"

This creates a virtual microphone called "PhoneMic" that any application can use as
an input device.

### Make It Persistent

To load these modules automatically on boot, add the two `pactl load-module` lines
to your PulseAudio configuration:

- PulseAudio: `~/.config/pulse/default.pa`
- PipeWire: `~/.config/pipewire/pipewire-pulse.conf.d/auroramic.conf`

### Verify

Check that the virtual microphone exists:

    pactl list sources short | grep PhoneMic

Then select "PhoneMic" as the input device in your application.
```

---

## 9. Version History

| Version | Date | Changes |
|---|---|---|
| v1.0.0 | TBD | Initial release |

---

## 10. Appendix: File Inventory

### Files to Create

| File | Purpose |
|---|---|
| `LICENSE` | MIT license |
| `.editorconfig` | Code style rules |
| `.github/workflows/ci.yml` | CI build + test + coverage pipeline |
| `AuroraMic.Tests/AuroraMic.Tests.fsproj` | Test project with Coverlet |
| `AuroraMic.Tests/ConfigTests.fs` | Server config load/save tests (11 tests) |
| `AuroraMic.Tests/ClientConfigTests.fs` | Client config IP persistence tests (6 tests) |
| `AuroraMic.Tests/NetworkInfoTests.fs` | IP enumeration tests (4 tests) |
| `AuroraMic.Tests/HandshakeTests.fs` | Server handshake validation tests (6 tests) |
| `AuroraMic.Tests/ClientHandshakeTests.fs` | Client handshake logic tests (6 tests) |
| `AuroraMic.Tests/AudioFormatTests.fs` | Audio format constants tests (5 tests) |
| `AuroraMic.Tests/ErrorHandlerTests.fs` | Error logging verification tests (2 tests) |
| `AuroraMic.Tests/IntegrationTests.fs` | Server lifecycle integration tests (3 tests) |

### Files to Modify

| File | Changes |
|---|---|
| `AuroraMic.Server/App.fs` | Replace `with _ ->` with logging, add ProcessExit cleanup |
| `AuroraMic.Client/App.fs` | Replace `with _ ->` with logging, add IP persistence, loading state, ProcessExit cleanup |
| `AuroraMic.Server/AuroraMic.Server.fsproj` | Align Avalonia 11.3.14, SoundFlow 1.4.1, remove unused packages |
| `AuroraMic.Client/AuroraMic.Client.fsproj` | Align Avalonia 11.3.14, SoundFlow 1.4.1, remove unused packages |
| `AuroraMic.Client.Android/AuroraMic.Client.Android.fsproj` | Fix package name, align Avalonia |
| `AuroraMic.Client.Android/Activities.fs` | Fix package name, add OnDestroy |
| `AuroraMic.Server.Desktop/Program.fs` | Add ProcessExit cleanup |
| `Readme.md` | Complete rewrite per spec |
| `AuroraMic.slnx` | Add test project reference |

### Files to Delete

| File | Reason |
|---|---|
| `AuroraMic.Server/Assets/avalonia-logo.ico` | Unused |
| `AuroraMic.Client/Assets/avalonia-logo.ico` | Unused |

---

## 11. Agent Certification

This document has been validated by the following agents:

| Agent | Scope | Status |
|---|---|---|
| Structure Agent | Project layout, dependencies, build config | Verified |
| Code Quality Agent | Error handling, security, performance, logging | Verified |
| UX Agent | UI/UX, accessibility, mobile, responsive | Verified |
| Protocol Agent | UDP protocol, handshake, audio format, threading | Verified |
| Linux Audio Agent | PulseAudio/PipeWire routing, pactl documentation | Verified |
| Conservative Validation Agent | Risk assessment, over-engineering check, minimum viable changes | Verified |
| Test Coverage Agent | 100% coverage strategy, test case design, CI enforcement | Verified |

All findings from these agents are incorporated into sections 2, 3, 4, and 6 of this document.

### Validation Summary

The conservative validation agent confirmed:

- **Audio pipeline works as-is.** No Opus, no header changes, no buffer pre-allocation needed.
- **Protocol works as-is.** No AUTH/PIN, no sequence numbers, no wire protocol break.
- **Project structure works as-is.** No file splitting, no compilation order risks.
- **FuncUI 1.5.2 stays.** No upgrade to 1.6.0 (API break risk).
- **Linux virtual mic:** README instructions are sufficient. In-app automation is v1.1.
- **Minimum viable change set:** 4 focused work sessions. LICENSE, package rename, logging, cleanup, README, CI.

The test coverage agent confirmed:

- **100% line coverage is achievable** without mocking SoundFlow. Business logic (config, handshake, network info, audio constants) is separable from hardware-dependent code.
- **43 automated tests** cover all automatable code paths. Manual checklist (16 tests) covers hardware-dependent scenarios.
- **CI enforcement** with Coverlet prevents regressions. Coverage below 100% fails the build.

The working software is the greatest asset. Protect it. Verify it. Ship it.

---

*This document is the source of truth for AuroraMic v1.0. Do not deviate without updating this file and re-running verification.*
