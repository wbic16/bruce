using Bruce.Core.Configuration;
using Bruce.Core.Enums;
using Bruce.Core.Interfaces;
using Bruce.Core.Models;
using Bruce.Core.Services;
using Xunit;

namespace Bruce.Core.Tests;

public class BruceEngineTests : IDisposable
{
    private readonly BruceEngine _engine;
    private readonly string _dataPath;

    public BruceEngineTests()
    {
        _dataPath = Path.Combine(Path.GetTempPath(), $"bruce_test_{Guid.NewGuid():N}");
        var config = new BruceConfig { DataPath = _dataPath, LogLevel = LogLevel.None };
        _engine = new BruceEngine(config);
    }

    public void Dispose()
    {
        _engine.Dispose();
        if (Directory.Exists(_dataPath))
        {
            Directory.Delete(_dataPath, recursive: true);
        }
    }

    // === Task Creation Tests ===

    [Fact]
    public void CreateTask_ValidInput_ReturnsTask()
    {
        var result = _engine.CreateTask("Test Task", "Description", TaskSource.Manual, TaskType.Adhoc);
        
        Assert.True(result.IsSuccess);
        Assert.Equal("Test Task", result.Value.Title);
        Assert.Equal(TaskState.Created, result.Value.State);
    }

    [Fact]
    public void CreateTask_EmptyTitle_ReturnsFailure()
    {
        var result = _engine.CreateTask("", "Description", TaskSource.Manual, TaskType.Adhoc);
        
        Assert.True(result.IsFailure);
        Assert.Contains("title", result.Error.ToLower());
    }

    [Fact]
    public void CreateTask_TitleTooLong_ReturnsFailure()
    {
        var longTitle = new string('x', 300);
        var result = _engine.CreateTask(longTitle, "Description", TaskSource.Manual, TaskType.Adhoc);
        
        Assert.True(result.IsFailure);
        Assert.Contains("200", result.Error); // Max length
    }

    [Fact]
    public void CreateTask_FiresEvent()
    {
        BruceTask? capturedTask = null;
        _engine.Events.SubscribeLegacy<TaskCreated>(e => capturedTask = e.Task);
        
        _engine.CreateTask("Test", "Desc", TaskSource.Manual, TaskType.Adhoc);
        
        Assert.NotNull(capturedTask);
        Assert.Equal("Test", capturedTask.Title);
    }

    // === Worker Creation Tests ===

    [Fact]
    public void CreateWorker_Human_ReturnsWorker()
    {
        var result = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker);
        
        Assert.True(result.IsSuccess);
        Assert.Equal("Alice", result.Value.Name);
        Assert.Equal(WorkerType.Human, result.Value.Type);
    }

    [Fact]
    public void CreateWorker_AI_RequiresSubstrate()
    {
        var result = _engine.CreateWorker("AI-1", WorkerType.AI, WorkerRole.Worker);
        
        Assert.True(result.IsFailure);
        Assert.Contains("substrate", result.Error.ToLower());
    }

    [Fact]
    public void CreateWorker_AIWithSubstrate_Succeeds()
    {
        var result = _engine.CreateWorker("Claude-1", WorkerType.AI, WorkerRole.Worker, "claude");
        
        Assert.True(result.IsSuccess);
        Assert.Equal("claude", result.Value.Substrate);
    }

    [Fact]
    public void CreateWorker_EmptyName_ReturnsFailure()
    {
        var result = _engine.CreateWorker("", WorkerType.Human, WorkerRole.Worker);
        
        Assert.True(result.IsFailure);
    }

    // === Task State Machine Tests ===

    [Fact]
    public void ReviewTask_FromCreated_TransitionsToReviewed()
    {
        var task = _engine.CreateTask("Test", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        
        var result = _engine.ReviewTask(task.Id);
        
        Assert.True(result.IsSuccess);
        var updated = _engine.GetTask(task.Id).Value;
        Assert.Equal(TaskState.Reviewed, updated.State);
    }

    [Fact]
    public void ReviewTask_InvalidId_ReturnsNotFound()
    {
        var result = _engine.ReviewTask("nonexistent");
        
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public void AdvanceTask_ThroughFullLifecycle()
    {
        // Create task and worker
        var task = _engine.CreateTask("Test", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        var worker = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker).Value;
        
        // Created -> Reviewed
        _engine.ReviewTask(task.Id);
        
        // Reviewed -> Assigned
        _engine.AssignTask(task.Id, worker.Id);
        
        // Assigned -> Testing
        var result = _engine.AdvanceTask(task.Id);
        Assert.True(result.IsSuccess);
        Assert.Equal(TaskState.Testing, _engine.GetTask(task.Id).Value.State);
        
        // Testing -> Done
        result = _engine.AdvanceTask(task.Id);
        Assert.True(result.IsSuccess);
        Assert.Equal(TaskState.Done, _engine.GetTask(task.Id).Value.State);
        
        // Done -> Cannot advance
        result = _engine.AdvanceTask(task.Id);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void GetValidTransitions_ReturnsCorrectStates()
    {
        var task = _engine.CreateTask("Test", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        
        var transitions = _engine.GetValidTransitions(task.Id);
        
        Assert.Contains(TaskState.Reviewed, transitions);
        Assert.DoesNotContain(TaskState.Assigned, transitions);
    }

    // === Assignment Tests ===

    [Fact]
    public void AssignTask_ReviewedTask_Succeeds()
    {
        var task = _engine.CreateTask("Test", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        var worker = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker).Value;
        _engine.ReviewTask(task.Id);
        
        var result = _engine.AssignTask(task.Id, worker.Id);
        
        Assert.True(result.IsSuccess);
        var updated = _engine.GetTask(task.Id).Value;
        Assert.Equal(TaskState.Assigned, updated.State);
        Assert.Equal(worker.Id, updated.AssignedTo);
    }

    [Fact]
    public void AssignTask_CreatedTask_AutoReviewsFirst()
    {
        var task = _engine.CreateTask("Test", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        var worker = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker).Value;
        
        // Assign directly without reviewing
        var result = _engine.AssignTask(task.Id, worker.Id);
        
        Assert.True(result.IsSuccess);
        var updated = _engine.GetTask(task.Id).Value;
        Assert.Equal(TaskState.Assigned, updated.State);
    }

    [Fact]
    public void AssignTask_InvalidWorkerId_ReturnsFailure()
    {
        var task = _engine.CreateTask("Test", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        
        var result = _engine.AssignTask(task.Id, "invalid-worker-id");
        
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ClaimTask_AvailableTask_Succeeds()
    {
        // Create and review task
        var task = _engine.CreateTask("Test", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        _engine.ReviewTask(task.Id);
        
        var worker = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker).Value;
        
        var result = _engine.ClaimTask(worker.Id);
        
        Assert.True(result.IsSuccess);
        Assert.Equal(task.Id, result.Value.Id);
    }

    [Fact]
    public void ClaimTask_NoAvailableTasks_ReturnsNotFound()
    {
        var worker = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker).Value;
        
        var result = _engine.ClaimTask(worker.Id);
        
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public void ClaimTask_RespectPreferredType()
    {
        var adhocTask = _engine.CreateTask("Adhoc", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        var plannedTask = _engine.CreateTask("Planned", "Desc", TaskSource.Manual, TaskType.Planned).Value;
        _engine.ReviewTask(adhocTask.Id);
        _engine.ReviewTask(plannedTask.Id);
        
        var worker = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker).Value;
        
        var result = _engine.ClaimTask(worker.Id, TaskType.Planned);
        
        Assert.True(result.IsSuccess);
        Assert.Equal(TaskType.Planned, result.Value.Type);
    }

    // === Capacity Tests ===

    [Fact]
    public void WorkerCapacity_WorkerRole_Has4Slots()
    {
        var worker = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker).Value;
        Assert.Equal(4, worker.MaxCapacity);
        Assert.Equal(2, worker.MaxAdhoc);
        Assert.Equal(2, worker.MaxPlanned);
    }

    [Fact]
    public void WorkerCapacity_ManagerRole_Has24Slots()
    {
        var worker = _engine.CreateWorker("Manager", WorkerType.Human, WorkerRole.Manager).Value;
        Assert.Equal(24, worker.MaxCapacity);
    }

    [Fact]
    public void AssignTask_ExceedsAdhocCapacity_ReturnsFailure()
    {
        var worker = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker).Value;
        
        // Fill adhoc slots (2 max for Worker)
        for (int i = 0; i < 2; i++)
        {
            var task = _engine.CreateTask($"Adhoc {i}", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
            _engine.ReviewTask(task.Id);
            _engine.AssignTask(task.Id, worker.Id);
        }
        
        // Try one more adhoc
        var extraTask = _engine.CreateTask("Extra", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        _engine.ReviewTask(extraTask.Id);
        var result = _engine.AssignTask(extraTask.Id, worker.Id);
        
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorCode.CapacityExceeded, result.ErrorCode);
    }

    [Fact]
    public void GetWorkerLoad_ReturnsCorrectCounts()
    {
        var worker = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker).Value;
        
        // Add tasks
        var adhoc = _engine.CreateTask("Adhoc", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        var planned = _engine.CreateTask("Planned", "Desc", TaskSource.Manual, TaskType.Planned).Value;
        _engine.ReviewTask(adhoc.Id);
        _engine.ReviewTask(planned.Id);
        _engine.AssignTask(adhoc.Id, worker.Id);
        _engine.AssignTask(planned.Id, worker.Id);
        
        var load = _engine.GetWorkerLoad(worker.Id);
        
        Assert.Equal(1, load.Adhoc);
        Assert.Equal(1, load.Planned);
        Assert.Equal(2, load.Total);
    }

    // === Messaging Tests ===

    [Fact]
    public void SendMessage_ValidInput_Succeeds()
    {
        var sender = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker).Value;
        var recipient = _engine.CreateWorker("Bob", WorkerType.Human, WorkerRole.Worker).Value;
        
        var result = _engine.SendMessage(sender.Id, "Hello!", recipient.Id);
        
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello!", result.Value.Body);
        Assert.Equal(sender.Id, result.Value.FromWorkerId);
        Assert.Equal(recipient.Id, result.Value.ToWorkerId);
    }

    [Fact]
    public void Broadcast_NoRecipient_CreatesBroadcast()
    {
        var sender = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker).Value;
        
        var result = _engine.Broadcast(sender.Id, "Team announcement");
        
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsBroadcast);
        Assert.Null(result.Value.ToWorkerId);
    }

    [Fact]
    public void SendMessage_EmptyBody_ReturnsFailure()
    {
        var sender = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker).Value;
        
        var result = _engine.SendMessage(sender.Id, "");
        
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void SendMessage_InvalidSender_ReturnsNotFound()
    {
        var result = _engine.SendMessage("invalid-id", "Hello");
        
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorCode.NotFound, result.ErrorCode);
    }

    // === System Summary Tests ===

    [Fact]
    public void GetSystemSummary_ReturnsAccurateCounts()
    {
        // Create some data
        _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker);
        _engine.CreateWorker("Claude", WorkerType.AI, WorkerRole.Worker, "claude");
        _engine.CreateTask("Task 1", "Desc", TaskSource.Manual, TaskType.Adhoc);
        _engine.CreateTask("Task 2", "Desc", TaskSource.Manual, TaskType.Planned);
        
        var summary = _engine.GetSystemSummary();
        
        Assert.Equal(2, summary.TotalWorkers);
        Assert.Equal(1, summary.HumanWorkers);
        Assert.Equal(1, summary.AiWorkers);
        Assert.Equal(2, summary.TotalTasks);
        Assert.Equal(2, summary.TasksByState_Created);
    }

    // === Event Tests ===

    [Fact]
    public void Events_TaskStateChanged_FiresOnTransition()
    {
        TaskStateChanged? capturedEvent = null;
        _engine.Events.SubscribeLegacy<TaskStateChanged>(e => capturedEvent = e);
        
        var task = _engine.CreateTask("Test", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        _engine.ReviewTask(task.Id);
        
        Assert.NotNull(capturedEvent);
        Assert.Equal(TaskState.Created, capturedEvent.OldState);
        Assert.Equal(TaskState.Reviewed, capturedEvent.NewState);
    }

    [Fact]
    public void Events_TaskAssigned_IndicatesPushOrClaim()
    {
        var events = new List<TaskAssigned>();
        _engine.Events.SubscribeLegacy<TaskAssigned>(e => events.Add(e));
        
        var task1 = _engine.CreateTask("Push", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        var task2 = _engine.CreateTask("Pull", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        var worker = _engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker).Value;
        _engine.ReviewTask(task1.Id);
        _engine.ReviewTask(task2.Id);
        
        _engine.AssignTask(task1.Id, worker.Id);  // Push
        _engine.ClaimTask(worker.Id);              // Pull
        
        Assert.Equal(2, events.Count);
        Assert.True(events[0].Pushed);
        Assert.False(events[1].Pushed);
    }
}

public class ConcurrencyTests : IDisposable
{
    private readonly BruceEngine _engine;
    private readonly string _dataPath;

    public ConcurrencyTests()
    {
        _dataPath = Path.Combine(Path.GetTempPath(), $"bruce_concurrent_{Guid.NewGuid():N}");
        var config = new BruceConfig { DataPath = _dataPath, LogLevel = LogLevel.None };
        _engine = new BruceEngine(config);
    }

    public void Dispose()
    {
        _engine.Dispose();
        if (Directory.Exists(_dataPath))
        {
            Directory.Delete(_dataPath, recursive: true);
        }
    }

    [Fact]
    public void ConcurrentClaims_OnlyOneSucceeds()
    {
        // Create single task
        var task = _engine.CreateTask("Contested Task", "Desc", TaskSource.Manual, TaskType.Adhoc).Value;
        _engine.ReviewTask(task.Id);
        
        // Create multiple workers
        var workers = Enumerable.Range(0, 5)
            .Select(i => _engine.CreateWorker($"Worker{i}", WorkerType.Human, WorkerRole.Worker).Value)
            .ToList();
        
        // All try to claim simultaneously
        var results = workers.AsParallel()
            .Select(w => _engine.ClaimTask(w.Id))
            .ToList();
        
        // Exactly one should succeed
        var successes = results.Count(r => r.IsSuccess);
        Assert.Equal(1, successes);
    }
}
