# Sprint 4 Questions & Design Decisions

## Day 741 - Questions for Review

### Design Decisions Made

#### 1. Phext Storage Layout

I chose a layout using **Series** as the entity type discriminator:

```
Library 1, Shelf 1:
  Series 1: Workers     (1.1.1/1.1.1/1.1.{n})
  Series 2: Tasks       (1.1.2/1.1.1/1.1.{n})
  Series 3: Assignments (1.1.3/1.1.1/1.1.{n})
  Series 4: Artifacts   (1.1.4/1.1.1/1.1.{n})
  Series 5: Messages    (1.1.5/1.1.1/1.1.{n})
```

**Questions:**
- Is this the coordinate layout you envisioned, or should entities live at different dimensional levels?
- Should we use higher dimensions (Library/Shelf) to partition data (e.g., by workspace or time)?

#### 2. Serialization Format

I implemented a simple `key=value` line format per scroll:

```
id=task-001
coord=2.1.1.1
title=Implement phext store
state=Created
...
```

**Questions:**
- Should we use a more structured format (e.g., TOML-like sections)?
- Is base64 encoding acceptable for binary artifact content?
- Should we support compression for large scrolls?

#### 3. Coordinate Allocation

Currently using incrementing scroll numbers within each series. The scroll counter persists across sessions by scanning existing coordinates on load.

**Questions:**
- Should deleted coordinates be reclaimed?
- Is there a max entities concern given 729 slots per entity type (9³)?
- Should we expand to use Section/Chapter for scaling?

#### 4. Phext Library Integration

I set up Bruce.Core.csproj with both options:
- **Project reference** (active): For development iteration
- **NuGet reference** (commented): For production deployment

**Question:** Once LibPhext is on NuGet, should we remove the project reference path entirely?

---

### Implementation Notes

#### What's Implemented
- [x] Full `PhextStore` implementing all store interfaces
- [x] Serialization/deserialization to phext scrolls
- [x] Optimistic concurrency (version checks)
- [x] Atomic writes with temp file + rename
- [x] Thread-safe with ReaderWriterLockSlim
- [x] Phext-specific operations (GetRawPhext, GetTextmap, FetchAt, Compact)

#### Not Yet Implemented
- [ ] Indexes for fast lookup (planned for Shelf 2)
- [ ] Migration from existing JSON data
- [ ] Batch operations
- [ ] Transaction support
- [ ] Phext-native queries (range fetch)

---

### Open Design Questions

1. **Migration Path**: Should we build a JSON→Phext migration tool, or assume clean start?

2. **Dual Store**: Should `BruceEngine` support switching between `JsonStore` and `PhextStore` via configuration?

3. **Navmap Integration**: Should Bruce expose an HTML navmap for browsing the phext structure?

4. **Checksum/Manifest**: Should we generate manifests for integrity checking? The `PhextEngine.Manifest()` function is available.

5. **Coordinate Display**: Should the UI show phext coordinates alongside BruceCoords? They serve different purposes.

6. **Archive Strategy**: When tasks complete, should they move to a different Series (Archive = Series 6) or different Shelf?

---

### Performance Considerations

The current implementation rebuilds and saves the entire phext file on each write. For production scale:

1. **Append-only log**: Write changes to a log, periodically compact
2. **Memory-mapped files**: For large phext documents
3. **Lazy loading**: Only parse scrolls on access
4. **Index files**: Separate index for ID→coordinate mapping

**Question:** At what scale should we consider these optimizations? Current approach is fine for hundreds of entities.

---

### Testing Needed

1. Round-trip serialization for all entity types
2. Concurrent access patterns
3. Large phext document handling
4. Coordinate allocation edge cases
5. Empty/malformed scroll handling

---

## Next Sprint Suggestions

If Sprint 4 goals are met, Sprint 5 could tackle:

1. JSON→Phext migration utility
2. Phext-native search/filter
3. Integration tests with full engine
4. Performance benchmarks
5. CLI commands for phext inspection (`bruce phext map`, `bruce phext fetch 1.1.2/1.1.1/1.1.5`)

---

*Questions compiled during Sprint 4 implementation. Please review and provide guidance for next iteration.*
