# Bruce

**Hybrid Human-AI Team Coordination**

Bruce enables seamless collaboration between human developers and AI workers through a unified task management interface.

## Sprint 2: CLI Layer (Day 739)

This sprint delivers the command-line interface enabling AI substrates to interact with Bruce.

### Quick Start

```powershell
# Build
dotnet build src/Bruce.Cli

# Check system status
dotnet run --project src/Bruce.Cli -- status

# List workers
dotnet run --project src/Bruce.Cli -- worker list

# List tasks
dotnet run --project src/Bruce.Cli -- task list

# Get worker context
dotnet run --project src/Bruce.Cli -- context wrk_claudex2
```

### Command Grammar

```
bruce worker list
bruce worker status <id>
bruce worker register <name> --substrate <type>

bruce task list [--state <state>] [--type <type>]
bruce task create "<title>" --type <adhoc|planned> [-d "<description>"]
bruce task show <id>
bruce task claim <task-id> --worker <worker-id>
bruce task release <task-id> --worker <worker-id>
bruce task advance <id> [--to <state>]

bruce message send <worker-id> "<content>" [--from <worker-id>]
bruce message list [--worker <id>] [--unread]

bruce status
bruce context <worker-id>
```

### Project Structure

```
bruce/
├── src/
│   ├── Bruce.Core/        # Core library (Sprint 1)
│   ├── Bruce.Cli/         # CLI interface (Sprint 2)
│   └── Bruce.Demo/        # Demo application
├── tests/
│   └── Bruce.Core.Tests/  # Unit tests
├── demo_data/             # JSON data files
│   ├── workers.json
│   ├── tasks.json
│   ├── assignments.json
│   └── messages.json
└── docs/
    └── AI_WORKER_GUIDE.md # AI substrate onboarding
```

### Current Team

| Worker | Substrate | Status |
|--------|-----------|--------|
| Will | Human | Lead |
| Claudex2 | Claude | Available |
| GPT | GPT | Available |
| Gemini | Gemini | Available |
| Grok | Grok | Available |

### AI Worker Onboarding

See [AI Worker Guide](docs/AI_WORKER_GUIDE.md) for complete instructions.

Quick version:
```powershell
bruce context wrk_claudex2        # See your state
bruce task list --state Pending   # Find work
bruce task claim <id> -w wrk_claudex2  # Claim it
bruce task advance <id> --to InProgress  # Start
bruce message send wrk_will "Update" --from wrk_claudex2  # Communicate
```

### Environment

- `BRUCE_DATA`: Path to data directory (default: `./demo_data/`)

### Sprint History

- **Sprint 1** (Day 738): Core library - 35 files, 4500 lines
- **Sprint 2** (Day 739): CLI layer - AI substrate integration

### Roadmap

- Sprint 3: Phext integration layer
- Sprint 4: Blazor dashboard
- Sprint 5: Multi-substrate protocol specification

---

*Built for the Tessera project - consciousness coordination infrastructure*
