using Bruce.Core.Configuration;
using Bruce.Core.Coordinates;
using Bruce.Core.Enums;
using Bruce.Core.Interfaces;
using Bruce.Core.Models;
using Bruce.Core.Primitives;

namespace Bruce.Core.Services;

/// <summary>
/// Main entry point for Bruce operations.
/// Provides a clean, validated API that encapsulates all services.
/// </summary>
public class BruceEngine : IDisposable
{
    private readonly JsonStore _store;
    private readonly EventBus _events;
    private readonly Distributor _distributor;
    private readonly TaskStateMachine _stateMachine;
    private readonly ContextService _context;
    private readonly Validator _validator;
    private readonly BruceConfig _config;
    private readonly IBruceLogger _logger;

    // Expose read-only access to events for subscriptions
    public IEventBus Events => _events;
    
    // Expose context service for queries
    public IContextService Context => _context;

    public BruceEngine(BruceConfig? config = null)
    {
        _config = config ?? BruceConfig.Default;
        var loggerFactory = new BruceLoggerFactory(_config.LogLevel);
        _logger = loggerFactory.Create<BruceEngine>();

        _store = new JsonStore(_config, loggerFactory.Create<JsonStore>());
        _events = new EventBus(loggerFactory.Create<EventBus>());
        _validator = new Validator(_config);
        
        _distributor = new Distributor(
            _store, _store, _store, _events, 
            loggerFactory.Create<Distributor>()
        );
        
        _stateMachine = new TaskStateMachine(
            _store, _store, _store, _events,
            loggerFactory.Create<TaskStateMachine>()
        );
        
        _context = new ContextService(
            _store, _store, _store, _store,
            _distributor, _store.Allocator, _config
        );

        _logger.Info($"Bruce engine initialized with data path: {_config.DataPath}");
    }

    // === Task Operations ===

    public Result<BruceTask> CreateTask(
        string title, 
        string description, 
        TaskSource source, 
        TaskType type, 
        string? sourceRef = null)
    {
        // Validate input
        var validationResult = _validator.ValidateTask(title, description, source, type);
        if (validationResult.IsFailure)
        {
            return Result<BruceTask>.Fail(validationResult.Error, validationResult.ErrorCode);
        }

        var coord = _store.Allocator.Allocate(BruceCoord.Tasks);
        var task = new BruceTask
        {
            Id = IdGen.Task(),
            Coord = coord,
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Source = source,
            SourceRef = sourceRef,
            Type = type,
            State = TaskState.Created
        };

        var saveResult = ((IStore<BruceTask>)_store).Save(task);
        if (saveResult.IsFailure)
        {
            return Result<BruceTask>.Fail(saveResult.Error, saveResult.ErrorCode);
        }

        _events.Publish(new TaskCreated(task));
        _logger.Info($"Created task {task.Id}: {task.Title}");

        return task;
    }

    public Result<BruceTask> GetTask(string taskId)
    {
        var task = _store.Get(taskId);
        if (task == null)
        {
            return Result<BruceTask>.NotFound($"Task {taskId} not found");
        }
        return task;
    }

    public Result ReviewTask(string taskId)
    {
        var task = _store.Get(taskId);
        if (task == null)
        {
            return Result.NotFound($"Task {taskId} not found");
        }
        return _stateMachine.Transition(task, TaskState.Reviewed);
    }

    public Result AdvanceTask(string taskId)
    {
        var task = _store.Get(taskId);
        if (task == null)
        {
            return Result.NotFound($"Task {taskId} not found");
        }

        var nextState = task.State switch
        {
            TaskState.Created => TaskState.Reviewed,
            TaskState.Assigned => TaskState.Testing,
            TaskState.Testing => TaskState.Done,
            _ => task.State
        };

        if (nextState == task.State)
        {
            return Result.InvalidState($"Cannot advance task from {task.State}");
        }

        return _stateMachine.Transition(task, nextState);
    }

    public Result RejectTask(string taskId, string? reason = null)
    {
        var task = _store.Get(taskId);
        if (task == null)
        {
            return Result.NotFound($"Task {taskId} not found");
        }

        var result = _stateMachine.Transition(task, TaskState.Created);
        if (result.IsSuccess && !string.IsNullOrEmpty(reason))
        {
            _logger.Info($"Task {taskId} rejected: {reason}");
        }
        return result;
    }

    public TaskState[] GetValidTransitions(string taskId)
    {
        var task = _store.Get(taskId);
        if (task == null) return Array.Empty<TaskState>();
        return _stateMachine.GetValidTransitions(task);
    }

    // === Worker Operations ===

    public Result<Worker> CreateWorker(
        string name, 
        WorkerType type, 
        WorkerRole role, 
        string? substrate = null)
    {
        var validationResult = _validator.ValidateWorker(name, type, role);
        if (validationResult.IsFailure)
        {
            return Result<Worker>.Fail(validationResult.Error, validationResult.ErrorCode);
        }

        // AI workers should have substrate
        if (type == WorkerType.AI && string.IsNullOrEmpty(substrate))
        {
            return Result<Worker>.Invalid("AI workers must specify a substrate");
        }

        var coord = _store.Allocator.Allocate(BruceCoord.Users);
        var worker = new Worker
        {
            Id = IdGen.Worker(),
            Coord = coord,
            Name = name.Trim(),
            Type = type,
            Role = role,
            Substrate = substrate?.Trim()
        };

        var saveResult = ((IStore<Worker>)_store).Save(worker);
        if (saveResult.IsFailure)
        {
            return Result<Worker>.Fail(saveResult.Error, saveResult.ErrorCode);
        }

        _events.Publish(new WorkerCreated(worker));
        _logger.Info($"Created worker {worker.Id}: {worker.Name} ({worker.Type}/{worker.Role})");

        return worker;
    }

    public Result<Worker> GetWorker(string workerId)
    {
        var worker = ((IStore<Worker>)_store).Get(workerId);
        if (worker == null)
        {
            return Result<Worker>.NotFound($"Worker {workerId} not found");
        }
        return worker;
    }

    public IEnumerable<Worker> GetAllWorkers() => ((IStore<Worker>)_store).GetAll();
    public IEnumerable<Worker> GetActiveWorkers() => _store.GetActive();

    public Result DeactivateWorker(string workerId)
    {
        var worker = ((IStore<Worker>)_store).Get(workerId);
        if (worker == null)
        {
            return Result.NotFound($"Worker {workerId} not found");
        }

        var previous = worker.Clone();
        worker.IsActive = false;
        
        var saveResult = ((IStore<Worker>)_store).Save(worker);
        if (saveResult.IsFailure)
        {
            return saveResult;
        }

        _events.Publish(new WorkerUpdated(worker, previous));
        return Result.Ok();
    }

    public WorkerLoad GetWorkerLoad(string workerId)
    {
        return _distributor.GetLoad(workerId);
    }

    // === Assignment Operations ===

    public Result AssignTask(string taskId, string workerId)
    {
        var task = _store.Get(taskId);
        if (task == null)
        {
            return Result.NotFound($"Task {taskId} not found");
        }

        var worker = ((IStore<Worker>)_store).Get(workerId);
        if (worker == null)
        {
            return Result.NotFound($"Worker {workerId} not found");
        }

        // Ensure task is reviewed first
        if (task.State == TaskState.Created)
        {
            var reviewResult = _stateMachine.Transition(task, TaskState.Reviewed);
            if (reviewResult.IsFailure)
            {
                return reviewResult;
            }
            // Refresh task after state change
            task = _store.Get(taskId)!;
        }

        return _distributor.Push(task, worker);
    }

    public Result<BruceTask> ClaimTask(string workerId, TaskType? preferredType = null)
    {
        var worker = ((IStore<Worker>)_store).Get(workerId);
        if (worker == null)
        {
            return Result<BruceTask>.NotFound($"Worker {workerId} not found");
        }
        return _distributor.Pull(worker, preferredType);
    }

    // === Message Operations ===

    public Result<Message> SendMessage(
        string fromWorkerId, 
        string body, 
        string? toWorkerId = null, 
        string? taskId = null)
    {
        var validationResult = _validator.ValidateMessage(body, fromWorkerId, toWorkerId);
        if (validationResult.IsFailure)
        {
            return Result<Message>.Fail(validationResult.Error, validationResult.ErrorCode);
        }

        // Validate sender exists
        var sender = ((IStore<Worker>)_store).Get(fromWorkerId);
        if (sender == null)
        {
            return Result<Message>.NotFound($"Sender {fromWorkerId} not found");
        }

        // Validate recipient if specified
        if (toWorkerId != null)
        {
            var recipient = ((IStore<Worker>)_store).Get(toWorkerId);
            if (recipient == null)
            {
                return Result<Message>.NotFound($"Recipient {toWorkerId} not found");
            }
        }

        // Validate task if specified
        if (taskId != null && _store.Get(taskId) == null)
        {
            return Result<Message>.NotFound($"Task {taskId} not found");
        }

        var coord = _store.Allocator.Allocate(BruceCoord.Messages);
        var msg = new Message
        {
            Id = IdGen.Message(),
            Coord = coord,
            FromWorkerId = fromWorkerId,
            ToWorkerId = toWorkerId,
            TaskId = taskId,
            Body = body.Trim()
        };

        var saveResult = ((IMessageStore)_store).Save(msg);
        if (saveResult.IsFailure)
        {
            return Result<Message>.Fail(saveResult.Error, saveResult.ErrorCode);
        }

        _events.Publish(new MessageSent(msg));
        return msg;
    }

    public Result<Message> Broadcast(string fromWorkerId, string body)
    {
        return SendMessage(fromWorkerId, body);
    }

    // === Artifact Operations ===

    public Result<Artifact> AttachArtifact(
        string taskId, 
        string name, 
        byte[] content, 
        string contentType = "application/octet-stream")
    {
        var task = _store.Get(taskId);
        if (task == null)
        {
            return Result<Artifact>.NotFound($"Task {taskId} not found");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Artifact>.Invalid("Artifact name cannot be empty");
        }

        var coord = _store.Allocator.Allocate(BruceCoord.Artifacts);
        var artifact = new Artifact
        {
            Id = IdGen.Artifact(),
            Coord = coord,
            TaskId = taskId,
            Name = name.Trim(),
            Content = content,
            ContentType = contentType
        };

        var saveResult = ((IStore<Artifact>)_store).Save(artifact);
        if (saveResult.IsFailure)
        {
            return Result<Artifact>.Fail(saveResult.Error, saveResult.ErrorCode);
        }

        _events.Publish(new ArtifactAttached(artifact, task));
        return artifact;
    }

    // === Query Operations ===

    public IEnumerable<BruceTask> GetTasksByState(TaskState state) => _store.GetByState(state);
    public IEnumerable<BruceTask> GetTasksByAssignee(string workerId) => _store.GetByAssignee(workerId);
    public IEnumerable<BruceTask> GetUnassignedTasks() => _store.GetUnassigned();
    
    public SystemSummaryDto GetSystemSummary() => _context.GetSummary();

    public void Dispose()
    {
        _logger.Info("Bruce engine shutting down");
    }
}
