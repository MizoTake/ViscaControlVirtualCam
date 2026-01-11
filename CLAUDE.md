# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**VISCA Control Virtual Camera** is a pure C# VISCA protocol server implementation for Unity that simulates a PTZ (Pan-Tilt-Zoom) camera rig. It receives VISCA commands via UDP/TCP from switchers (like Blackmagic ATEM) and controls a virtual camera rig in Unity.

The project is structured as a UPM package with:
- **Core domain logic:** Protocol parsing, command handling, motion model (zero Unity dependencies)
- **Network layer:** UDP/TCP server with concurrent client management
- **Unity integration:** MonoBehaviours that bridge core logic to transforms and cameras
- **Comprehensive tests:** 5+ NUnit test classes covering protocol, parsing, and motion

## Essential Commands

### Building & Running

**Open in Editor:**
- Use Unity Hub with version from `ProjectSettings/ProjectVersion.txt` (currently 2022.3.62f2)
- Open the project and load `Assets/ViscaControlVirtualCamera/Scenes/PTZ_Sample.unity` to test

**Run Tests (EditMode):**
```bash
"<UnityPath>\Unity.exe" -batchmode -quit -projectPath "<repo>" -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml -logFile Logs/tests.log
```

**Build Standalone:**
```bash
"<UnityPath>\Unity.exe" -batchmode -quit -projectPath "<repo>" -executeMethod BuildScript.BuildStandalone -logFile Logs/build.log
```

### Development Workflow

1. **Tests are in:** `Packages/com.mizotake.viscavirtualcam/Tests/EditMode/`
   - Run individual test class via Unity Test Runner (Window → General → Test Runner)
   - Or run all EditMode tests via command above
2. **Scripts are in:** `Packages/com.mizotake.viscavirtualcam/Runtime/` and `Editor/`
3. **Sample scene:** `Assets/ViscaControlVirtualCamera/Scenes/PTZ_Sample.unity` demonstrates full integration
4. **Presets:** Created via `Create > Visca > PTZ Settings` or `Tools > Visca > Create PTZ Presets (Indoor/Outdoor/Fast)`

## High-Level Architecture

### Core Processing Flow

```
Network Input (UDP/TCP)
  ↓
ViscaFrameFramer (accumulates & extracts 0xFF-terminated frames)
  ↓
ViscaCommandRegistry.Parse() (O(1) lookup of command parsers)
  ↓
ViscaCommandContext (immutable command data with all parameters)
  ↓
PtzViscaHandler.Handle() (dispatches to PtzModel commands, queues to main thread)
  ↓
PtzModel.Step() (computes motion deltas each frame)
  ↓
PtzControllerBehaviour.Update() (applies deltas to transforms/camera)
  ↓
Network Response (ACK/Completion packets)
```

### Key Architectural Decisions

1. **Pure C# Core with Thin Behaviours**
   - Core logic (`PtzModel`, `ViscaServerCore`, `PtzViscaHandler`) has zero Unity dependencies
   - Enables testing and reuse outside Unity
   - Behaviours are thin adapters: `ViscaServerBehaviour` and `PtzControllerBehaviour`

2. **Main Thread Dispatching**
   - Network threads queue commands to `ConcurrentQueue<Action>`
   - Dequeued in `ViscaServerBehaviour.Update()` for safe Transform/Camera access
   - Prevents multi-threading race conditions

3. **Immutable Command Context Pattern**
   - `ViscaCommandContext` struct captures all command data upfront
   - Enables safe closure captures in lambdas queued to main thread
   - Reduces allocation pressure

4. **Dual-Lookup Command Registry**
   - Fast path: O(1) exact byte-pattern match via `ViscaCommandKey`
   - Fallback: Linear scan for variable-length commands
   - 18+ standard VISCA commands registered

5. **Motion Model Separation**
   - `PtzModel` is pure state machine (no I/O)
   - `Step(yaw, pitch, fov, deltaTime)` returns deltas each frame
   - Speed mapping uses configurable gamma function for non-linear response
   - Supports acceleration limits and position inversion flags

6. **Cancellation via Generation Counter**
   - Incremented on each cancel command
   - Operations check generation before completing
   - Avoids orphaned completion responses from pre-cancel commands
   - Thread-safe via `Interlocked`

7. **Memory Presets with Persistence**
   - Dictionary of named positions (0x00-0xFF)
   - Loaded/saved via `IPlayerPrefsAdapter` interface
   - Can use Unity PlayerPrefs or custom implementation

### Key Classes & Their Roles

| Class | Role |
|-------|------|
| `ViscaProtocol` | Constants & socket ID extraction (protocol reference) |
| `ViscaCommandRegistry` | Command parser registry with 18+ standard commands |
| `ViscaCommandContext` | Immutable command data + factory methods per command type |
| `ViscaCommandType` | Enum of 21 supported command types |
| `ViscaFrameFramer` | Protocol framing: byte accumulation & 0xFF frame extraction |
| `ViscaServerCore` | UDP/TCP server with concurrent client management |
| `PtzModel` | State machine: position tracking, motion calculations, presets |
| `PtzViscaHandler` | Main handler: dispatches VISCA commands to PtzModel |
| `PtzSettings` | ScriptableObject: motion limits, speed mapping gamma, inversion flags |
| `ViscaServerBehaviour` | MonoBehaviour: integrates server + main thread dispatch |
| `PtzControllerBehaviour` | MonoBehaviour: applies PtzModel deltas to transforms |

### Command Processing Pattern

Example: Pan/Tilt Drive command

```
1. Network: "81 01 06 01 [speed_pan] [speed_tilt] [pan_dir] [tilt_dir] FF" arrives
2. ViscaFrameFramer extracts frame
3. ViscaCommandRegistry matches bytes [1]=0x01, [2]=0x06, [3]=0x01 → PanTiltDrive parser
4. Parser extracts speeds (0x01-0x18) and directions (0x01/0x02/0x03)
5. ViscaCommandContext.PanTiltDrive() creates immutable context
6. PtzViscaHandler.Handle() matches ViscaCommandType.PanTiltDrive
7. TryEnqueue() adds lambda: ctx => Model.CommandPanTiltVariable(...)
8. Lambda executes on main thread in Update()
9. PtzModel.Step() applies velocity deltas each frame
10. PtzControllerBehaviour.Update() applies deltas to transforms
11. ViscaResponse.SendAck() + SendCompletion() sent back to client
```

### Test Locations & Strategy

**Test Files:**
- `Packages/com.mizotake.viscavirtualcam/Tests/EditMode/PtzModelTests.cs` — motion calculations
- `Packages/com.mizotake.viscavirtualcam/Tests/EditMode/ViscaFrameFramerTests.cs` — frame extraction
- `Packages/com.mizotake.viscavirtualcam/Tests/EditMode/ViscaParserTests.cs` — nibble decoding
- `Packages/com.mizotake.viscavirtualcam/Tests/EditMode/ViscaResponseTests.cs` — response formatting
- `Packages/com.mizotake.viscavirtualcam/Tests/EditMode/ViscaServerCoreTests.cs` — command parsing & registry

**Testing Approach:**
- Deterministic: no scene dependencies, tests use dependency injection
- Mocking: `MockPlayerPrefsAdapter` for preset persistence testing
- Frame format validation: exact VISCA protocol compliance
- Edge cases: buffer overflow, command cancellation, position wrapping

## Code Organization & Conventions

### Directory Structure

```
Packages/com.mizotake.viscavirtualcam/
├── Runtime/
│   ├── Behaviours/          # Unity MonoBehaviour adapters
│   ├── Core/                # Pure C# domain logic
│   │   └── Commands/        # VISCA command registry & parsing
│   ├── Net/                 # Network layer (UDP/TCP)
│   └── Unity/               # Unity-specific adapters
├── Editor/                  # Editor-only tools
├── Tests/
│   └── EditMode/            # NUnit test suite
└── Documentation~/          # Protocol & feature docs

Assets/ViscaControlVirtualCamera/
├── Scripts/                 # Application-level scripts
├── Scenes/                  # Sample scenes
├── Presets/                 # PTZ settings assets
└── Docs/                    # Additional documentation
```

### Naming & Style

- **Namespaces:** `ViscaControlVirtualCam.*` (e.g., `ViscaControlVirtualCam.Core.Commands`)
- **Types:** `PascalCase` (e.g., `PtzModel`, `ViscaServerCore`)
- **Methods/Properties:** `PascalCase` (public), `camelCase` (parameters/locals)
- **Private Fields:** `_camelCase` (e.g., `_frameBuffer`)
- **Constants:** `UPPER_SNAKE` (protocol constants only)
- **Braces:** Allman style (opening brace on new line)
- **Indentation:** 4 spaces, UTF-8
- **One public type per file** (exceptions: minor helper types)

### Command Implementation Checklist

When adding a new VISCA command:

1. **Define in `ViscaCommandType` enum** (Core/Commands/ViscaCommandType.cs)
2. **Add factory method to `ViscaCommandContext`** (Core/Commands/ViscaCommandContext.cs)
3. **Register parser in `ViscaCommandRegistry`** (Core/Commands/ViscaCommandRegistry.cs)
   - Exact match: set `key = new ViscaCommandKey(frame[1], frame[2], frame[3])`
   - Variable length: add to list for fallback scan
4. **Add handler in `PtzViscaHandler.Handle()`** (Behaviours/PtzViscaHandler.cs)
   - Pattern match on `ViscaCommandType`
   - Call appropriate `PtzModel.Command*()` method
5. **Implement in `PtzModel`** (Core/PtzModel.cs)
   - Add public `CommandXyz()` method
   - Modify model state
   - Queue responses via responder callback
6. **Write tests** (Tests/EditMode/ViscaServerCoreTests.cs or new test class)
   - Frame format validation
   - Parameter extraction
   - Response packet format
7. **Update documentation** (Packages/.../Documentation~/ and README.md)

## Important Implementation Notes

### Network Communication

- **Default Ports:** UDP 52381, TCP 52380 (configurable via `ViscaServerBehaviour` inspector)
- **Frame Format:** 3-16 bytes, 0xFF-terminated
- **Socket ID:** Extracted from frame[0] low nibble; responses echo it in 0x90/0xA0 header
- **VISCA-IP Header:** 8-byte prefix (0x01 0x10 seq_hi seq_lo ...) in responses mirrors incoming sequence

### Speed Mapping

- VISCA speed bytes: 0x01-0x18 (decimal 1-24)
- Converted to degrees/second via: `speed_deg_s = pow(normalized_byte / 24, gamma) * max_deg_s`
- Gamma default: 1.0 (linear); <1.0 for finer low-speed control
- Configurable per-axis in `PtzSettings` and `PtzTuningProfile`

### Command Cancellation

- Command: `8X 01 04 FF` (where X is socket)
- Responses: `90 6z 04 FF` (CommandCanceled)
- Pending operations increment `cancellationGeneration` counter
- Queued lambdas check generation before completing to avoid orphaned replies

### Motion Limiting

- Pan range: -180 to +180 degrees (configurable in `PtzSettings`)
- Tilt range: -90 to +90 degrees (configurable)
- Zoom FOV: min/max FOV range (configurable)
- Acceleration limits: optional per-axis (default 600 deg/s²)
- Position inversion: separate flags for variable (joystick) vs. absolute (lerp) control

### Thread Safety

- **Network threads:** Only call `ViscaServerCore.HandleFrame()` and responder callbacks
- **Main thread:** Only call `PtzModel` methods and modify Transforms
- **Synchronization:** `ConcurrentQueue<Action>` queues commands to main thread
- **Atomic:** Cancellation counter uses `Interlocked.Increment()`

### Memory Presets

- Stored as `Dictionary<int, PtzPreset>` in `PtzModel` (keyed by 0x00-0xFF)
- Persisted via `IPlayerPrefsAdapter` (implemented by default as `UnityPlayerPrefsAdapter`)
- Loaded on `PtzControllerBehaviour.Awake()`
- Saved on each `CommandMemorySet()` call
- Can be reset via `CommandMemoryReset()`

## Important Protocol References

- **Sony FR7 VISCA over IP:** [ILME-FR7_OH_5042055031](https://www.sony.jp/professional/support/manual_pdf/c_c/ILME-FR7_OH_5042055031.pdf)
- **Standard VISCA Commands:** Pan/Tilt Drive, Pan/Tilt Absolute, Zoom Variable/Direct, Focus Variable/Direct, Iris Variable/Direct, Memory Recall/Set, Inquiry, CommandCancel
- **Blackmagic Extensions:** Compatible with ATEM switchers (Zoom Direct, Focus/Iris Direct, Memory Recall)

## When Editing Code

- Scope edits to `Packages/com.mizotake.viscavirtualcam/` unless explicitly requested
- Keep `.meta` files; Unity uses them for GUID tracking
- If you add/modify commands, update docs in `Packages/com.mizotake.viscavirtualcam/Documentation~/`
- Tests should mirror target file names with `Tests` suffix (e.g., `PtzModelTests.cs`)
- Run tests before committing: new features must pass existing tests
- Follow Conventional Commits: `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, etc.

## Quick Start for New Contributors

1. **Understand the flow:** Read `ViscaServerBehaviour` → `PtzViscaHandler` → `PtzModel.Step()`
2. **Check command registration:** Browse `ViscaCommandRegistry` to see existing command patterns
3. **Run tests:** Open Test Runner (Window → General → Test Runner) to verify everything works
4. **Load sample scene:** Open `PTZ_Sample.unity` and press Play to see integration in action
5. **Inspect with logging:** Enable `Verbose Log` and `Log Received Commands` in `ViscaServerBehaviour` inspector to see command details
6. **Modify presets:** Try changing `PtzSettings` assets to see how speed/position limits affect behavior
