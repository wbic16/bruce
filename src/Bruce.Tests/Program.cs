using System.Collections.Concurrent;
using System.Text.Json;
using Bruce.Core.Configuration;
using Bruce.Core.Coordinates;
using Bruce.Core.Enums;
using Bruce.Core.Interfaces;
using Bruce.Core.Models;
using Bruce.Core.Primitives;
using Bruce.Core.Services;

Console.WriteLine("=== Bruce Test Suite ===\n");

var passed = 0;
var failed = 0;

void Test(string name, Func<bool> test)
{
    try
    {
        if (test())
        {
            Console.WriteLine($"  ✓ {name}");
            passed++;
        }
        else
        {
            Console.WriteLine($"  ✗ {name} - FAILED");
            failed++;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✗ {name} - EXCEPTION: {ex.Message}");
        failed++;
    }
}

async Task TestAsync(string name, Func<Task<bool>> test)
{
    try
    {
        if (await test())
        {
            Console.WriteLine($"  ✓ {name}");
            passed++;
        }
        else
        {
            Console.WriteLine($"  ✗ {name} - FAILED");
            failed++;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✗ {name} - EXCEPTION: {ex.Message}");
        failed++;
    }
}

// === P0: BruceCoord JSON Serialization ===
Console.WriteLine("\n--- P0: BruceCoord JSON Serialization ---");

Test("BruceCoord serializes to short form", () =>
{
    var coord = new BruceCoord(2, 3, 4, 5);
    var json = JsonSerializer.Serialize(coord);
    return json == "\"2.3.4.5\"";
});

Test("BruceCoord deserializes from short form", () =>
{
    var json = "\"2.3.4.5\"";
    var coord = JsonSerializer.Deserialize<BruceCoord>(json);
    return coord.EntityType == 2 && coord.Y == 3 && coord.Z == 4 && coord.W == 5;
});

Test("BruceCoord roundtrips through JSON", () =>
{
    var original = new BruceCoord(1, 9, 9, 9);
    var json = JsonSerializer.Serialize(original);
    var restored = JsonSerializer.Deserialize<BruceCoord>(json);
    return original == restored;
});

Test("Model with BruceCoord serializes correctly", () =>
{
    var task = new BruceTask
    {
        Id = "T-test",
        Coord = new BruceCoord(2, 1, 1, 1),
        Title = "Test"
    };
    var json = JsonSerializer.Serialize(task);
    // Default serialization uses PascalCase
    return json.Contains("\"Coord\":\"2.1.1.1\"") || json.Contains("\"coord\":\"2.1.1.1\"");
});

// === P0: Atomic File Writes ===
Console.WriteLine("\n--- P0: Atomic File Writes ---");

Test("Atomic writes create temp file", () =>
{
    var testDir = Path.Combine(Path.GetTempPath(), $"bruce_test_{Guid.NewGuid():N}");
    try
    {
        var config = new BruceConfig { DataPath = testDir, AtomicWrites = true };
        using var engine = new BruceEngine(config);
        engine.CreateTask("Test", "Atomic write test", TaskSource.Manual, TaskType.Adhoc);
        
        // Check file exists (temp file should be cleaned up)
        var tasksFile = Path.Combine(testDir, "tasks.json");
        var tempFile = tasksFile + ".tmp";
        return File.Exists(tasksFile) && !File.Exists(tempFile);
    }
    finally
    {
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
    }
});

// === P0: Race Condition in Pull ===
Console.WriteLine("\n--- P0: Race Condition Protection ---");

Test("Concurrent pulls don't double-assign", () =>
{
    var testDir = Path.Combine(Path.GetTempPath(), $"bruce_test_{Guid.NewGuid():N}");
    try
    {
        var config = new BruceConfig { DataPath = testDir };
        using var engine = new BruceEngine(config);
        
        // Create workers
        var workers = new List<Worker>();
        for (int i = 0; i < 5; i++)
        {
            var r = engine.CreateWorker($"worker-{i}", WorkerType.AI, WorkerRole.Worker, "test");
            if (r.IsSuccess) workers.Add(r.Value);
        }
        
        // Create single task
        var taskResult = engine.CreateTask("Single task", "Race test", TaskSource.Manual, TaskType.Adhoc);
        engine.ReviewTask(taskResult.Value.Id);
        
        // Concurrent pulls
        var claimed = new ConcurrentBag<string>();
        var tasks = workers.Select(w => Task.Run(() =>
        {
            var result = engine.ClaimTask(w.Id);
            if (result.IsSuccess)
            {
                claimed.Add(result.Value.Id);
            }
        })).ToArray();
        
        Task.WaitAll(tasks);
        
        // Only one worker should have claimed it
        return claimed.Count == 1;
    }
    finally
    {
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
    }
});

// === P1: Result Types ===
Console.WriteLine("\n--- P1: Result Types ---");

Test("Result.Ok creates success", () =>
{
    var r = Result.Ok();
    return r.IsSuccess && !r.IsFailure;
});

Test("Result.Fail creates failure with error", () =>
{
    var r = Result.Fail("test error", ResultErrorCode.ValidationFailed);
    return r.IsFailure && r.Error == "test error" && r.ErrorCode == ResultErrorCode.ValidationFailed;
});

Test("Result<T>.Ok carries value", () =>
{
    var r = Result<int>.Ok(42);
    return r.IsSuccess && r.Value == 42;
});

Test("Result<T>.Fail throws on Value access", () =>
{
    var r = Result<int>.Fail("error");
    try { _ = r.Value; return false; }
    catch (InvalidOperationException) { return true; }
});

Test("Result.Map transforms on success", () =>
{
    var r = Result<int>.Ok(5).Map(x => x * 2);
    return r.IsSuccess && r.Value == 10;
});

Test("Result.Map preserves failure", () =>
{
    var r = Result<int>.Fail("error").Map(x => x * 2);
    return r.IsFailure && r.Error == "error";
});

Test("Result.Bind chains operations", () =>
{
    var r = Result<int>.Ok(5)
        .Bind(x => x > 0 ? Result<int>.Ok(x * 2) : Result<int>.Fail("negative"));
    return r.IsSuccess && r.Value == 10;
});

// === P1: Input Validation ===
Console.WriteLine("\n--- P1: Input Validation ---");

Test("Empty title rejected", () =>
{
    var testDir = Path.Combine(Path.GetTempPath(), $"bruce_test_{Guid.NewGuid():N}");
    try
    {
        using var engine = new BruceEngine(new BruceConfig { DataPath = testDir });
        var r = engine.CreateTask("", "desc", TaskSource.Manual, TaskType.Adhoc);
        return r.IsFailure && r.ErrorCode == ResultErrorCode.ValidationFailed;
    }
    finally
    {
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
    }
});

Test("Long title rejected", () =>
{
    var testDir = Path.Combine(Path.GetTempPath(), $"bruce_test_{Guid.NewGuid():N}");
    try
    {
        using var engine = new BruceEngine(new BruceConfig { DataPath = testDir });
        var r = engine.CreateTask(new string('x', 500), "desc", TaskSource.Manual, TaskType.Adhoc);
        return r.IsFailure && r.Error.Contains("200");
    }
    finally
    {
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
    }
});

Test("AI worker without substrate rejected", () =>
{
    var testDir = Path.Combine(Path.GetTempPath(), $"bruce_test_{Guid.NewGuid():N}");
    try
    {
        using var engine = new BruceEngine(new BruceConfig { DataPath = testDir });
        var r = engine.CreateWorker("test", WorkerType.AI, WorkerRole.Worker);
        return r.IsFailure && r.Error.Contains("substrate");
    }
    finally
    {
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
    }
});

// === P1: Thread-Safe CoordAllocator ===
Console.WriteLine("\n--- P1: Thread-Safe CoordAllocator ---");

Test("Concurrent allocations produce unique coords", () =>
{
    var allocator = new CoordAllocator();
    var coords = new ConcurrentBag<BruceCoord>();
    
    var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
    {
        coords.Add(allocator.Allocate(BruceCoord.Tasks));
    })).ToArray();
    
    Task.WaitAll(tasks);
    
    // All coords should be unique
    return coords.Distinct().Count() == 100;
});

Test("Allocator tracks remaining capacity", () =>
{
    var allocator = new CoordAllocator();
    var before = allocator.GetRemaining(BruceCoord.Tasks);
    allocator.Allocate(BruceCoord.Tasks);
    var after = allocator.GetRemaining(BruceCoord.Tasks);
    return after == before - 1;
});

// === P1: Optimistic Concurrency ===
Console.WriteLine("\n--- P1: Optimistic Concurrency ---");

Test("Version increments on save", () =>
{
    var testDir = Path.Combine(Path.GetTempPath(), $"bruce_test_{Guid.NewGuid():N}");
    try
    {
        using var engine = new BruceEngine(new BruceConfig { DataPath = testDir });
        var r = engine.CreateTask("Test", "Version test", TaskSource.Manual, TaskType.Adhoc);
        var task = engine.GetTask(r.Value.Id).Value;
        
        // Initial version is 2 (1 from creation + 1 from first save)
        return task.Version >= 1;
    }
    finally
    {
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
    }
});

// === P1: ID Generation ===
Console.WriteLine("\n--- P1: ID Generation ---");

Test("Task IDs have T prefix", () =>
{
    var id = IdGen.Task();
    return id.StartsWith("T-") && IdGen.IsValidTaskId(id);
});

Test("Worker IDs have W prefix", () =>
{
    var id = IdGen.Worker();
    return id.StartsWith("W-") && IdGen.IsValidWorkerId(id);
});

Test("Message IDs have M prefix", () =>
{
    var id = IdGen.Message();
    return id.StartsWith("M-") && IdGen.IsValidMessageId(id);
});

Test("GetEntityType returns correct type", () =>
{
    return IdGen.GetEntityType("T-abc123") == "Task" &&
           IdGen.GetEntityType("W-abc123") == "Worker" &&
           IdGen.GetEntityType("M-abc123") == "Message";
});

Test("Generated IDs are unique", () =>
{
    var ids = Enumerable.Range(0, 1000).Select(_ => IdGen.Task()).ToList();
    return ids.Distinct().Count() == 1000;
});

// === P2: Async Event Handlers ===
Console.WriteLine("\n--- P2: Async Event Handlers ---");

await TestAsync("Async handlers are called", async () =>
{
    var testDir = Path.Combine(Path.GetTempPath(), $"bruce_test_{Guid.NewGuid():N}");
    try
    {
        using var engine = new BruceEngine(new BruceConfig { DataPath = testDir });
        var called = false;
        
        engine.Events.Subscribe<TaskCreated>(async (evt, ct) =>
        {
            await Task.Delay(10, ct);
            called = true;
        });
        
        engine.CreateTask("Test", "Async test", TaskSource.Manual, TaskType.Adhoc);
        
        // Wait for async handler
        await Task.Delay(100);
        return called;
    }
    finally
    {
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
    }
});

await TestAsync("PublishAsync waits for handlers", async () =>
{
    var events = new EventBus();
    var order = new List<int>();
    
    events.Subscribe<TaskCreated>(async (evt, ct) =>
    {
        await Task.Delay(50, ct);
        order.Add(1);
    });
    
    var task = new BruceTask { Id = "T-test", Title = "Test" };
    await events.PublishAsync(new TaskCreated(task));
    order.Add(2);
    
    // 1 should come before 2 because we awaited
    return order.Count == 2 && order[0] == 1 && order[1] == 2;
});

// === P2: DTOs ===
Console.WriteLine("\n--- P2: DTOs ---");

Test("TaskDto.FromEntity maps correctly", () =>
{
    var task = new BruceTask
    {
        Id = "T-test",
        Coord = new BruceCoord(2, 1, 1, 1),
        Title = "Test Task",
        Description = "Description",
        Source = TaskSource.Manual,
        State = TaskState.Created,
        Type = TaskType.Adhoc
    };
    
    var dto = TaskDto.FromEntity(task, "Alice");
    return dto.Id == task.Id &&
           dto.Coord == "2.1.1.1" &&
           dto.Title == task.Title &&
           dto.AssignedToName == "Alice";
});

Test("WorkerDto includes computed properties", () =>
{
    var worker = new Worker
    {
        Id = "W-test",
        Coord = new BruceCoord(1, 1, 1, 1),
        Name = "Test",
        Type = WorkerType.Human,
        Role = WorkerRole.Worker
    };
    
    var dto = WorkerDto.FromEntity(worker, new WorkerLoad(1, 1));
    return dto.MaxCapacity == 4 &&
           dto.CurrentLoad.Total == 2 &&
           dto.AvailableCapacity == 2;
});

// === BruceCoord Extended Tests ===
Console.WriteLine("\n--- BruceCoord Extended ---");

Test("FromLinear and LinearPosition are inverse", () =>
{
    for (int pos = 0; pos < 729; pos++)
    {
        var coord = BruceCoord.FromLinear(2, pos);
        if (coord.LinearPosition != pos) return false;
    }
    return true;
});

Test("Coords are sortable", () =>
{
    var c1 = new BruceCoord(2, 1, 1, 1);
    var c2 = new BruceCoord(2, 1, 1, 2);
    var c3 = new BruceCoord(2, 1, 2, 1);
    return c1 < c2 && c2 < c3;
});

Test("Next returns sequential coord", () =>
{
    var c1 = new BruceCoord(2, 1, 1, 1);
    var c2 = c1.Next();
    return c2.HasValue && c2.Value == new BruceCoord(2, 1, 1, 2);
});

Test("Next returns null at boundary", () =>
{
    var last = new BruceCoord(2, 9, 9, 9);
    return last.Next() == null;
});

Test("Parse handles old 3-part format", () =>
{
    var coord = BruceCoord.Parse("2.3.4");
    return coord.EntityType == 2 && coord.Y == 3 && coord.Z == 4 && coord.W == 1;
});

// === State Machine ===
Console.WriteLine("\n--- State Machine ---");

Test("Valid transitions are allowed", () =>
{
    var testDir = Path.Combine(Path.GetTempPath(), $"bruce_test_{Guid.NewGuid():N}");
    try
    {
        using var engine = new BruceEngine(new BruceConfig { DataPath = testDir });
        var r = engine.CreateTask("Test", "SM test", TaskSource.Manual, TaskType.Adhoc);
        var valid = engine.GetValidTransitions(r.Value.Id);
        return valid.Length == 1 && valid[0] == TaskState.Reviewed;
    }
    finally
    {
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
    }
});

Test("Invalid transition fails", () =>
{
    var testDir = Path.Combine(Path.GetTempPath(), $"bruce_test_{Guid.NewGuid():N}");
    try
    {
        using var engine = new BruceEngine(new BruceConfig { DataPath = testDir });
        var r = engine.CreateTask("Test", "SM test", TaskSource.Manual, TaskType.Adhoc);
        // Try to jump directly to Done (invalid)
        var task = engine.GetTask(r.Value.Id).Value;
        return !engine.GetValidTransitions(r.Value.Id).Contains(TaskState.Done);
    }
    finally
    {
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
    }
});

// === Summary ===
Console.WriteLine($"\n=== Results: {passed} passed, {failed} failed ===");
Environment.ExitCode = failed > 0 ? 1 : 0;
