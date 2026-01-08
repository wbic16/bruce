# Sprint 4 Changelog - Day 741

## Summary

Replaced JSON-based persistence with native phext storage, enabling coordinate-addressed data management for Bruce.

---

## New Features

### 1. LibPhext NuGet Package Configuration

**File:** `libphext-cs/src/Phext/Phext.csproj`

- Added full NuGet package metadata
- Enabled automatic package generation on build
- Configured Source Link for debugging
- Added symbol package (.snupkg) support
- Included README in package

**Version:** 0.3.0

### 2. PhextStore - Phext-Based Persistence

**File:** `bruce/src/Bruce.Core/Services/PhextStore.cs`

A complete phext-based storage implementation that:

- Implements all store interfaces (ITaskStore, IWorkerStore, IAssignmentStore, IArtifactStore, IMessageStore)
- Uses phext coordinates for entity addressing
- Serializes entities to key=value scroll format
- Supports optimistic concurrency with version checking
- Thread-safe with ReaderWriterLockSlim
- Atomic file writes for data safety

**Storage Layout:**
```
Library 1, Shelf 1:
  Series 1: Workers
  Series 2: Tasks  
  Series 3: Assignments
  Series 4: Artifacts
  Series 5: Messages
```

### 3. StoreFactory

**File:** `bruce/src/Bruce.Core/Services/StoreFactory.cs`

- Factory pattern for creating storage backends
- Switch between JSON and Phext storage via configuration
- Migration utility: `MigrateJsonToPhext()`

### 4. Configuration Updates

**File:** `bruce/src/Bruce.Core/Configuration/BruceConfig.cs`

- Added `StoreType` enum (Json, Phext)
- Default storage now Phext
- Added `BruceConfig.Legacy` for JSON fallback

---

## Files Modified

| File | Change |
|------|--------|
| `libphext-cs/src/Phext/Phext.csproj` | NuGet package configuration |
| `bruce/src/Bruce.Core/Bruce.Core.csproj` | Project reference to Phext |
| `bruce/src/Bruce.Core/Configuration/BruceConfig.cs` | StoreType option |

## Files Added

| File | Description |
|------|-------------|
| `bruce/src/Bruce.Core/Services/PhextStore.cs` | Phext storage implementation |
| `bruce/src/Bruce.Core/Services/StoreFactory.cs` | Store factory with migration |
| `bruce/tests/Bruce.Core.Tests/PhextStoreTests.cs` | Unit tests |
| `docs/NUGET_INSTRUCTIONS.md` | NuGet publishing guide |
| `docs/SPRINT4_QUESTIONS.md` | Design questions for review |
| `docs/SPRINT4_CHANGELOG.md` | This file |

---

## Breaking Changes

- Default storage changed from JSON to Phext
- Use `BruceConfig.Legacy` or `StoreType = StoreType.Json` for backward compatibility

---

## Migration Guide

### From JSON to Phext

```csharp
var result = StoreFactory.MigrateJsonToPhext("bruce_data");
if (result.Success)
{
    Console.WriteLine($"Migrated {result.TotalMigrated} entities");
}
```

### Using Legacy JSON Storage

```csharp
var config = BruceConfig.Builder()
    .WithStoreType(StoreType.Json)
    .Build();
```

---

## Testing

Run tests:
```bash
cd bruce
dotnet test
```

New tests in `PhextStoreTests.cs`:
- Task CRUD operations
- Worker persistence
- Message handling with newlines
- Phext-specific operations (GetRawPhext, GetTextmap, Compact)
- Persistence across reloads
- Concurrency conflict detection

---

## Next Steps (Sprint 5 candidates)

1. [ ] Publish LibPhext to NuGet.org
2. [ ] Run full test suite
3. [ ] JSON → Phext migration CLI command
4. [ ] Performance benchmarks
5. [ ] Phext-native search/filter queries
6. [ ] CLI: `bruce phext map`, `bruce phext fetch`

---

## Technical Debt

- [ ] Add indexes for fast ID→coordinate lookup
- [ ] Implement batch operations
- [ ] Consider append-only log for large datasets
- [ ] Add compression for large scrolls

---

*Sprint 4 completed: Day 741*
