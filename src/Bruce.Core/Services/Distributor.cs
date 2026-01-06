using Bruce.Core.Configuration;
using Bruce.Core.Enums;
using Bruce.Core.Interfaces;
using Bruce.Core.Models;
using Bruce.Core.Primitives;

namespace Bruce.Core.Services;

/// <summary>
/// Task distribution service with push/pull support.
/// Thread-safe with optimistic concurrency for pull operations.
/// </summary>
public class Distributor : IDistributor
{
    private readonly ITaskStore _tasks;
    private readonly IWorkerStore _workers;
    private readonly IAssignmentStore _assignments;
    private readonly IEventBus _events;
    private readonly IBruceLogger _logger;
    private readonly object _pullLock = new();  // Serialize pull operations

    public Distributor(
        ITaskStore tasks,
        IWorkerStore workers,
        IAssignmentStore assignments,
        IEventBus events,
        IBruceLogger? logger = null)
    {
        _tasks = tasks;
        _workers = workers;
        _assignments = assignments;
        _events = events;
        _logger = logger ?? NullLogger.Instance;
    }

    public Result Push(BruceTask task, Worker worker)
    {
        // Validate state
        if (task.State != TaskState.Reviewed)
        {
            return Result.InvalidState($"Cannot assign task in state {task.State}. Must be Reviewed.");
        }

        if (!worker.IsActive)
        {
            return Result.Invalid($"Worker {worker.Name} is not active");
        }

        // Check capacity
        if (!CanAccept(worker, task.Type))
        {
            var load = GetLoad(worker);
            return Result.CapacityExceeded(
                $"Worker {worker.Name} at capacity ({load.Total}/{worker.MaxCapacity})"
            );
        }

        // Update task
        task.State = TaskState.Assigned;
        task.AssignedTo = worker.Id;
        
        var saveResult = ((IStore<BruceTask>)_tasks).Save(task);
        if (saveResult.IsFailure)
        {
            return saveResult;
        }

        // Create assignment
        var assignment = new Assignment
        {
            Id = IdGen.Assignment(),
            TaskId = task.Id,
            WorkerId = worker.Id,
            PushedAt = DateTime.UtcNow,
            Coord = task.Coord  // Share coord with task
        };
        
        var assignResult = _assignments.Save(assignment);
        if (assignResult.IsFailure)
        {
            _logger.Error($"Failed to save assignment for task {task.Id}");
        }

        _events.Publish(new TaskAssigned(task, worker, Pushed: true));
        _events.Publish(new TaskStateChanged(task, TaskState.Reviewed, TaskState.Assigned));

        _logger.Info($"Pushed task {task.Id} to {worker.Name}");
        return Result.Ok();
    }

    public Result<BruceTask> Pull(Worker worker, TaskType? preferredType = null)
    {
        if (!worker.IsActive)
        {
            return Result<BruceTask>.Invalid($"Worker {worker.Name} is not active");
        }

        // Serialize pull operations to prevent race conditions
        lock (_pullLock)
        {
            return PullInternal(worker, preferredType);
        }
    }

    private Result<BruceTask> PullInternal(Worker worker, TaskType? preferredType)
    {
        // Find available reviewed tasks
        var available = _tasks.GetUnassigned()
            .OrderBy(t => t.Created)
            .ToList();

        if (available.Count == 0)
        {
            return Result<BruceTask>.NotFound("No tasks available to claim");
        }

        BruceTask? taskToClaim = null;

        if (preferredType.HasValue)
        {
            // Try preferred type first
            taskToClaim = available.FirstOrDefault(t => 
                t.Type == preferredType.Value && CanAccept(worker, t.Type));

            // Fall back to any type
            taskToClaim ??= available.FirstOrDefault(t => 
                t.Type != preferredType.Value && CanAccept(worker, t.Type));
        }
        else
        {
            taskToClaim = available.FirstOrDefault(t => CanAccept(worker, t.Type));
        }

        if (taskToClaim == null)
        {
            var load = GetLoad(worker);
            return Result<BruceTask>.CapacityExceeded(
                $"Worker {worker.Name} cannot accept any available tasks ({load.Total}/{worker.MaxCapacity})"
            );
        }

        // Double-check task is still unassigned (optimistic concurrency)
        var freshTask = _tasks.Get(taskToClaim.Id);
        if (freshTask == null || freshTask.AssignedTo != null || freshTask.State != TaskState.Reviewed)
        {
            // Task was claimed by someone else, try again
            _logger.Debug($"Task {taskToClaim.Id} was claimed by another worker, retrying");
            return PullInternal(worker, preferredType);  // Recursive retry
        }

        // Claim the task
        freshTask.State = TaskState.Assigned;
        freshTask.AssignedTo = worker.Id;

        var saveResult = ((IStore<BruceTask>)_tasks).Save(freshTask);
        if (saveResult.IsFailure)
        {
            if (saveResult.ErrorCode == ResultErrorCode.ConcurrencyConflict)
            {
                // Another worker claimed it, retry
                _logger.Debug($"Concurrency conflict claiming task {freshTask.Id}, retrying");
                return PullInternal(worker, preferredType);
            }
            return Result<BruceTask>.Fail(saveResult.Error, saveResult.ErrorCode);
        }

        // Create assignment
        var assignment = new Assignment
        {
            Id = IdGen.Assignment(),
            TaskId = freshTask.Id,
            WorkerId = worker.Id,
            ClaimedAt = DateTime.UtcNow,
            Coord = freshTask.Coord
        };
        _assignments.Save(assignment);

        _events.Publish(new TaskAssigned(freshTask, worker, Pushed: false));
        _events.Publish(new TaskStateChanged(freshTask, TaskState.Reviewed, TaskState.Assigned));

        _logger.Info($"Worker {worker.Name} claimed task {freshTask.Id}");
        return freshTask;
    }

    public WorkerLoad GetLoad(Worker worker)
    {
        return GetLoad(worker.Id);
    }

    public WorkerLoad GetLoad(string workerId)
    {
        var active = _assignments.GetByWorker(workerId)
            .Where(a => a.CompletedAt == null)
            .Select(a => _tasks.Get(a.TaskId))
            .Where(t => t != null)
            .ToList();

        var adhoc = active.Count(t => t!.Type == TaskType.Adhoc);
        var planned = active.Count(t => t!.Type == TaskType.Planned);

        return new WorkerLoad(adhoc, planned);
    }

    public bool CanAccept(Worker worker, TaskType taskType)
    {
        if (!worker.IsActive) return false;

        var load = GetLoad(worker);

        if (worker.Role == WorkerRole.Worker)
        {
            // Workers have typed slots
            return taskType switch
            {
                TaskType.Adhoc => load.Adhoc < worker.MaxAdhoc,
                TaskType.Planned => load.Planned < worker.MaxPlanned,
                _ => false
            };
        }

        // Managers and Directors just have total capacity
        return load.Total < worker.MaxCapacity;
    }
}
