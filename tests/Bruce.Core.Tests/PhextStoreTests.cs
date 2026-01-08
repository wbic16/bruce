// ===========================================================================================================
// Bruce.Core.Tests: PhextStoreTests
// Unit tests for phext-based persistence layer
// Sprint 4, Day 741
// ===========================================================================================================

using Bruce.Core.Configuration;
using Bruce.Core.Coordinates;
using Bruce.Core.Enums;
using Bruce.Core.Interfaces;
using Bruce.Core.Models;
using Bruce.Core.Primitives;
using Bruce.Core.Services;
using Xunit;

namespace Bruce.Core.Tests;

public class PhextStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly PhextStore _store;

    public PhextStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"bruce_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _store = new PhextStore(new BruceConfig { DataPath = _testDir, AtomicWrites = true });
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    #region Task Tests

    [Fact]
    public void Save_NewTask_AssignsCoordinateAndPersists()
    {
        // Arrange
        var task = new BruceTask
        {
            Id = "task-001",
            Coord = new BruceCoord(BruceCoord.Tasks, 1, 1, 1),
            Title = "Test Task",
            Description = "A test task",
            Source = TaskSource.Manual,
            State = TaskState.Created,
            Type = TaskType.Adhoc
        };

        // Act
        var result = ((IStore<BruceTask>)_store).Save(task);

        // Assert
        Assert.True(result.Success, result.Error);
        var retrieved = _store.Get("task-001");
        Assert.NotNull(retrieved);
        Assert.Equal("Test Task", retrieved.Title);
        Assert.Equal(2, retrieved.Version); // Incremented on save
    }

    [Fact]
    public void Save_ExistingTask_UpdatesAndIncrementsVersion()
    {
        // Arrange
        var task = new BruceTask
        {
            Id = "task-002",
            Coord = new BruceCoord(BruceCoord.Tasks, 1, 1, 2),
            Title = "Original Title",
            State = TaskState.Created
        };
        ((IStore<BruceTask>)_store).Save(task);

        // Act
        var updated = _store.Get("task-002")!;
        updated.Title = "Updated Title";
        updated.State = TaskState.Reviewed;
        var result = ((IStore<BruceTask>)_store).Save(updated);

        // Assert
        Assert.True(result.Success);
        var retrieved = _store.Get("task-002");
        Assert.Equal("Updated Title", retrieved!.Title);
        Assert.Equal(TaskState.Reviewed, retrieved.State);
        Assert.Equal(3, retrieved.Version);
    }

    [Fact]
    public void Save_ConcurrencyConflict_ReturnsError()
    {
        // Arrange
        var task = new BruceTask
        {
            Id = "task-003",
            Coord = new BruceCoord(BruceCoord.Tasks, 1, 1, 3),
            Title = "Conflict Test"
        };
        ((IStore<BruceTask>)_store).Save(task);

        // Simulate concurrent modification
        var copy1 = _store.Get("task-003")!;
        var copy2 = _store.Get("task-003")!;
        
        copy1.Title = "Update 1";
        ((IStore<BruceTask>)_store).Save(copy1);

        // Act - copy2 now has stale version
        copy2.Title = "Update 2";
        var result = ((IStore<BruceTask>)_store).Save(copy2);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ResultErrorCode.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public void GetByState_ReturnsMatchingTasks()
    {
        // Arrange
        ((IStore<BruceTask>)_store).Save(new BruceTask { Id = "t1", Coord = new BruceCoord(2,1,1,1), State = TaskState.Created });
        ((IStore<BruceTask>)_store).Save(new BruceTask { Id = "t2", Coord = new BruceCoord(2,1,1,2), State = TaskState.Reviewed });
        ((IStore<BruceTask>)_store).Save(new BruceTask { Id = "t3", Coord = new BruceCoord(2,1,1,3), State = TaskState.Created });

        // Act
        var created = _store.GetByState(TaskState.Created).ToList();
        var reviewed = _store.GetByState(TaskState.Reviewed).ToList();

        // Assert
        Assert.Equal(2, created.Count);
        Assert.Single(reviewed);
        Assert.Equal("t2", reviewed[0].Id);
    }

    [Fact]
    public void Delete_RemovesTaskFromStore()
    {
        // Arrange
        var task = new BruceTask { Id = "delete-me", Coord = new BruceCoord(2,1,2,1) };
        ((IStore<BruceTask>)_store).Save(task);

        // Act
        var result = ((IStore<BruceTask>)_store).Delete("delete-me");

        // Assert
        Assert.True(result.Success);
        Assert.Null(_store.Get("delete-me"));
    }

    #endregion

    #region Worker Tests

    [Fact]
    public void Save_Worker_PersistsAllFields()
    {
        // Arrange
        var worker = new Worker
        {
            Id = "claude-1",
            Coord = new BruceCoord(BruceCoord.Users, 1, 1, 1),
            Name = "Claude (Opus 4.5)",
            Type = WorkerType.AI,
            Role = WorkerRole.Worker,
            Substrate = "claude",
            IsActive = true
        };

        // Act
        ((IStore<Worker>)_store).Save(worker);
        var retrieved = ((IStore<Worker>)_store).Get("claude-1");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Claude (Opus 4.5)", retrieved.Name);
        Assert.Equal(WorkerType.AI, retrieved.Type);
        Assert.Equal("claude", retrieved.Substrate);
        Assert.True(retrieved.IsActive);
    }

    [Fact]
    public void GetActive_ReturnsOnlyActiveWorkers()
    {
        // Arrange
        ((IStore<Worker>)_store).Save(new Worker { Id = "w1", Coord = new BruceCoord(1,1,1,1), IsActive = true });
        ((IStore<Worker>)_store).Save(new Worker { Id = "w2", Coord = new BruceCoord(1,1,1,2), IsActive = false });
        ((IStore<Worker>)_store).Save(new Worker { Id = "w3", Coord = new BruceCoord(1,1,1,3), IsActive = true });

        // Act
        var active = _store.GetActive().ToList();

        // Assert
        Assert.Equal(2, active.Count);
        Assert.All(active, w => Assert.True(w.IsActive));
    }

    #endregion

    #region Message Tests

    [Fact]
    public void Save_Message_SupportsNewlines()
    {
        // Arrange
        var msg = new Message
        {
            Id = "msg-001",
            Coord = new BruceCoord(BruceCoord.Messages, 1, 1, 1),
            FromWorkerId = "user-1",
            Body = "Line 1\nLine 2\nLine 3"
        };

        // Act
        ((IMessageStore)_store).Save(msg);
        var retrieved = ((IMessageStore)_store).Get("msg-001");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Line 1\nLine 2\nLine 3", retrieved.Body);
    }

    [Fact]
    public void GetRecent_ReturnsInDescendingOrder()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        ((IMessageStore)_store).Save(new Message { Id = "m1", Coord = new BruceCoord(4,1,1,1), Timestamp = baseTime.AddMinutes(-30) });
        ((IMessageStore)_store).Save(new Message { Id = "m2", Coord = new BruceCoord(4,1,1,2), Timestamp = baseTime.AddMinutes(-10) });
        ((IMessageStore)_store).Save(new Message { Id = "m3", Coord = new BruceCoord(4,1,1,3), Timestamp = baseTime });

        // Act
        var recent = ((IMessageStore)_store).GetRecent(2).ToList();

        // Assert
        Assert.Equal(2, recent.Count);
        Assert.Equal("m3", recent[0].Id);
        Assert.Equal("m2", recent[1].Id);
    }

    #endregion

    #region Phext-Specific Tests

    [Fact]
    public void GetRawPhext_ReturnsPhextDocument()
    {
        // Arrange
        ((IStore<BruceTask>)_store).Save(new BruceTask { Id = "t1", Coord = new BruceCoord(2,1,1,1), Title = "Test" });

        // Act
        var phext = _store.GetRawPhext();

        // Assert
        Assert.False(string.IsNullOrEmpty(phext));
        Assert.Contains("id=t1", phext);
        Assert.Contains("title=Test", phext);
    }

    [Fact]
    public void GetTextmap_ReturnsSummaryOfCoordinates()
    {
        // Arrange
        ((IStore<BruceTask>)_store).Save(new BruceTask { Id = "t1", Coord = new BruceCoord(2,1,1,1), Title = "Task One" });
        ((IStore<Worker>)_store).Save(new Worker { Id = "w1", Coord = new BruceCoord(1,1,1,1), Name = "Worker One" });

        // Act
        var textmap = _store.GetTextmap();

        // Assert
        Assert.False(string.IsNullOrEmpty(textmap));
    }

    [Fact]
    public void Compact_RebuildsPhextDocument()
    {
        // Arrange
        ((IStore<BruceTask>)_store).Save(new BruceTask { Id = "t1", Coord = new BruceCoord(2,1,1,1), Title = "Keep" });
        ((IStore<BruceTask>)_store).Save(new BruceTask { Id = "t2", Coord = new BruceCoord(2,1,1,2), Title = "Delete" });
        ((IStore<BruceTask>)_store).Delete("t2");

        // Act
        _store.Compact();

        // Assert
        var phext = _store.GetRawPhext();
        Assert.Contains("Keep", phext);
    }

    #endregion

    #region Persistence Tests

    [Fact]
    public void Reload_PreservesData()
    {
        // Arrange
        ((IStore<BruceTask>)_store).Save(new BruceTask { Id = "persist-1", Coord = new BruceCoord(2,1,1,1), Title = "Persisted" });
        ((IStore<Worker>)_store).Save(new Worker { Id = "persist-w1", Coord = new BruceCoord(1,1,1,1), Name = "Persisted Worker" });

        // Act - Create new store instance from same path
        var store2 = new PhextStore(new BruceConfig { DataPath = _testDir });
        var task = store2.Get("persist-1");
        var worker = ((IStore<Worker>)store2).Get("persist-w1");

        // Assert
        Assert.NotNull(task);
        Assert.Equal("Persisted", task.Title);
        Assert.NotNull(worker);
        Assert.Equal("Persisted Worker", worker.Name);
    }

    #endregion
}
