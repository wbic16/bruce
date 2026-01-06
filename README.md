# Bruce - DevOps Task Coordinator

Internal tool for real-time task distribution across hybrid human-AI teams.

## Sprint 1 Deliverable

**Status:** ✅ Complete (35/35 tests passing)

### Quick Start

```bash
# Build
dotnet build

# Run demo
dotnet run --project src/Bruce.Demo

# Run tests
dotnet run --project src/Bruce.Tests
```

### Architecture

```
Bruce.Core/
├── Configuration/     # BruceConfig, Logging
├── Coordinates/       # Phext-compatible BruceCoord (1.1.1/1.1.X/Y.Z.W)
├── Enums/            # TaskState, TaskType, TaskSource, WorkerType, WorkerRole
├── Interfaces/       # Store + Service contracts
├── Models/           # Entities (mutable) + DTOs (immutable)
├── Primitives/       # Result<T>, Guard, IdGen
└── Services/         # BruceEngine, JsonStore, Distributor, EventBus, etc.
```

### Core Concepts

**Workers:** Human or AI, with roles (Worker/Manager/Director) that determine capacity.
- Worker: 2 adhoc + 2 planned slots (4 total)
- Manager: 24 any-type slots
- Director: 200 any-type slots

**Tasks:** Flow through states: Created → Reviewed → Assigned → Testing → Done

**Distribution:**
- Push: Coordinator assigns task to specific worker
- Pull: Worker claims next available task matching their capacity

**Addressing:** Phext coordinates at `1.1.1/1.1.X/Y.Z.W` where X = entity type:
- 1 = Users (729 slots)
- 2 = Tasks (729 slots)
- 3 = Artifacts
- 4 = Messages
- 5 = Archive

### API Examples

```csharp
using var engine = new BruceEngine();

// Create worker
var worker = engine.CreateWorker("alice", WorkerType.Human, WorkerRole.Worker);

// Create and review task
var task = engine.CreateTask("Fix bug", "Details...", TaskSource.Manual, TaskType.Adhoc);
engine.ReviewTask(task.Value.Id);

// Assign (push)
engine.AssignTask(task.Value.Id, worker.Value.Id);

// Or let worker claim (pull)
var claimed = engine.ClaimTask(worker.Value.Id, TaskType.Adhoc);

// Check result
claimed.Match(
    t => Console.WriteLine($"Claimed: {t.Title}"),
    (err, code) => Console.WriteLine($"Error: {err}")
);

// Advance through states
engine.AdvanceTask(task.Value.Id);  // → Testing
engine.AdvanceTask(task.Value.Id);  // → Done
```

### Key Features (Sprint 1)

- [x] Result monad for explicit error handling
- [x] Input validation with aggregated errors
- [x] Optimistic concurrency (version tracking)
- [x] Thread-safe coordinate allocation
- [x] Atomic file writes (crash-safe)
- [x] Race condition protection in task claiming
- [x] Async event handlers (SignalR-ready)
- [x] Prefixed IDs (T-, W-, M-, F-, A-)
- [x] Immutable DTOs for external consumption
- [x] Configurable logging

### Data Persistence

JSON files in configurable directory (default: `bruce_data/`):
- `tasks.json`
- `workers.json`
- `assignments.json`
- `messages.json`
- `artifacts.json`

### Configuration

```csharp
var config = BruceConfig.Builder()
    .WithDataPath("my_data")
    .WithLogLevel(LogLevel.Debug)
    .WithAtomicWrites(true)
    .Build();

using var engine = new BruceEngine(config);
```

### Sprint 2 Options

1. **CLI tool** - `bruce task create`, `bruce claim`, `bruce status`
2. **Blazor Server** - Real-time dashboard with SignalR
3. **API ingest** - Teams/Email/Helix webhooks
4. **libphext-cs integration** - Native phext persistence

---

*Built for the Exocortex of 2130.*
