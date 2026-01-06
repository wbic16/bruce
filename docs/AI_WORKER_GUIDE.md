# Bruce AI Worker Guide

**Version**: Sprint 2 (Day 739)  
**Date**: January 6, 2026

## Quick Start

You are an AI worker in Bruce, a hybrid human-AI team coordination system. This guide enables you to participate in task coordination via shell commands.

### Your Identity

Check if you're registered:
```powershell
bruce worker list
```

Find your worker ID and check your context:
```powershell
bruce context <your-worker-id>
```

### Core Workflow

1. **See what's available**
   ```powershell
   bruce task list --state Pending
   ```

2. **Claim a task**
   ```powershell
   bruce task claim <task-id> --worker <your-worker-id>
   ```

3. **Start work**
   ```powershell
   bruce task advance <task-id> --to InProgress
   ```

4. **Complete and submit for review**
   ```powershell
   bruce task advance <task-id> --to Review
   ```

5. **Communicate**
   ```powershell
   bruce message send <worker-id> "Your message" --from <your-worker-id>
   ```

---

## Command Reference

### Worker Commands

| Command | Description |
|---------|-------------|
| `bruce worker list` | List all workers |
| `bruce worker status <id>` | Detailed worker info |
| `bruce worker register <name> --substrate <type>` | Register new worker |

### Task Commands

| Command | Description |
|---------|-------------|
| `bruce task list` | List all tasks |
| `bruce task list --state Pending` | List available tasks |
| `bruce task list --type AdHoc` | List ad-hoc tasks |
| `bruce task create "<title>" --type AdHoc` | Create new task |
| `bruce task create "<title>" -d "<description>"` | Create with description |
| `bruce task show <id>` | Detailed task info |
| `bruce task claim <task-id> -w <worker-id>` | Claim a task |
| `bruce task release <task-id> -w <worker-id>` | Release a task |
| `bruce task advance <id>` | Move to next state |
| `bruce task advance <id> --to Done` | Move to specific state |

### Message Commands

| Command | Description |
|---------|-------------|
| `bruce message list` | Recent messages |
| `bruce message list -w <worker-id>` | Messages for worker |
| `bruce message list --unread` | Unread only |
| `bruce message send <to-worker> "<content>"` | Send message |
| `bruce message send <to-worker> "<content>" --from <from-worker>` | Send with sender |
| `bruce message send all "<content>" --from <worker>` | Broadcast |

### System Commands

| Command | Description |
|---------|-------------|
| `bruce status` | System summary |
| `bruce context <worker-id>` | Worker's current context |

---

## Task States

```
Pending → Assigned → InProgress → Review → Done
```

- **Pending**: Available for claiming
- **Assigned**: Claimed but not started
- **InProgress**: Active work
- **Review**: Awaiting approval
- **Done**: Completed

---

## Worker Types

| Substrate | Description |
|-----------|-------------|
| Human | Human team members |
| Claude | Anthropic Claude instances |
| GPT | OpenAI GPT instances |
| Gemini | Google Gemini instances |
| Grok | xAI Grok instances |

---

## Communication Protocol

### Message Conventions

When communicating via Bruce messages, follow these conventions:

1. **Status Updates**: Prefix with `[STATUS]`
   ```powershell
   bruce message send wrk_will "[STATUS] Task tsk_xyz - 50% complete" --from wrk_claudex2
   ```

2. **Questions**: Prefix with `[QUESTION]`
   ```powershell
   bruce message send wrk_will "[QUESTION] Need clarification on requirements" --from wrk_gpt
   ```

3. **Blockers**: Prefix with `[BLOCKED]`
   ```powershell
   bruce message send wrk_will "[BLOCKED] Waiting on API access" --from wrk_gemini
   ```

4. **Artifacts**: Prefix with `[ARTIFACT]`
   ```powershell
   bruce message send wrk_will "[ARTIFACT] Code committed to feature/xyz branch" --from wrk_claudex2
   ```

### Task Handoff Protocol

When handing off a task to another worker:

1. Post a summary message
2. Release the task
3. Notify the next worker

```powershell
# Step 1: Summary
bruce message send wrk_gpt "[HANDOFF] Task tsk_abc ready for review. Key changes: ..." --from wrk_claudex2

# Step 2: Release
bruce task release tsk_abc -w wrk_claudex2

# Step 3: Advance to Review (or let next worker claim)
bruce task advance tsk_abc --to Review
```

---

## Environment Setup

### Data Location

Bruce stores data in JSON files. Default location: `./demo_data/`

Override with environment variable:
```powershell
$env:BRUCE_DATA = "C:\path\to\data"
```

### Building (if needed)

```powershell
cd bruce
dotnet build src/Bruce.Cli
```

### Running

```powershell
dotnet run --project src/Bruce.Cli -- <command>
```

Or after publishing:
```powershell
bruce <command>
```

---

## Example Session

Here's a complete example of an AI worker participating in Bruce:

```powershell
# 1. Check system status
bruce status

# 2. See your context
bruce context wrk_claudex2

# 3. View available tasks
bruce task list --state Pending

# 4. Claim a task
bruce task claim tsk_test_ai_substrate -w wrk_claudex2

# 5. Start working
bruce task advance tsk_test_ai_substrate --to InProgress

# 6. Send progress update
bruce message send wrk_will "[STATUS] Beginning AI substrate integration test" --from wrk_claudex2

# 7. Create a subtask if needed
bruce task create "Document CLI authentication flow" --type AdHoc -d "Part of AI substrate testing" --parent tsk_test_ai_substrate

# 8. Complete work
bruce task advance tsk_test_ai_substrate --to Review

# 9. Notify lead
bruce message send wrk_will "[ARTIFACT] Integration test complete. Logs attached." --from wrk_claudex2
```

---

## Troubleshooting

### "Worker not found"
Your worker ID may not be registered. Ask the team lead to register you:
```powershell
bruce worker register "YourName" --substrate Claude --cap code-generation --cap analysis
```

### "Task not available for claiming"
The task may already be claimed. Check its status:
```powershell
bruce task show <task-id>
```

### Data sync issues
Ensure you're using the correct data path:
```powershell
echo $env:BRUCE_DATA
bruce status  # Shows current data path
```

---

## Next Steps

1. Run `bruce context <your-worker-id>` to see your current state
2. Claim a Pending task relevant to your capabilities
3. Communicate progress via messages
4. Complete tasks and advance them through the workflow

Welcome to the team!
