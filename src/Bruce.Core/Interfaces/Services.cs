using Bruce.Core.Enums;
using Bruce.Core.Models;
using Bruce.Core.Primitives;

namespace Bruce.Core.Interfaces;

/// <summary>
/// Task distribution service
/// </summary>
public interface IDistributor
{
    /// <summary>Coordinator pushes task to specific worker</summary>
    Result Push(BruceTask task, Worker worker);

    /// <summary>Worker pulls next available task matching their capacity</summary>
    Result<BruceTask> Pull(Worker worker, TaskType? preferredType = null);

    /// <summary>Get current load for a worker</summary>
    WorkerLoad GetLoad(Worker worker);
    WorkerLoad GetLoad(string workerId);

    /// <summary>Check if worker can accept task</summary>
    bool CanAccept(Worker worker, TaskType taskType);
}

/// <summary>
/// Task state machine
/// </summary>
public interface ITaskStateMachine
{
    bool CanTransition(BruceTask task, TaskState newState);
    Result Transition(BruceTask task, TaskState newState);
    TaskState[] GetValidTransitions(BruceTask task);
}

/// <summary>
/// Context aggregation service
/// </summary>
public interface IContextService
{
    /// <summary>Build current context snapshot for a worker</summary>
    WorkerContext GetContext(string workerId);

    /// <summary>Get shared context across all workers</summary>
    WorkerContext GetSharedContext();
    
    /// <summary>Get system summary</summary>
    SystemSummaryDto GetSummary();
}

/// <summary>
/// Validation service
/// </summary>
public interface IValidator
{
    Result ValidateTask(string title, string description, TaskSource source, TaskType type);
    Result ValidateWorker(string name, WorkerType type, WorkerRole role);
    Result ValidateMessage(string body, string fromWorkerId, string? toWorkerId);
}
