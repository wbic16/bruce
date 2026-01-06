using Bruce.Core.Configuration;
using Bruce.Core.Enums;
using Bruce.Core.Interfaces;
using Bruce.Core.Models;
using Bruce.Core.Primitives;

namespace Bruce.Core.Services;

/// <summary>
/// Manages task state transitions with validation.
/// </summary>
public class TaskStateMachine : ITaskStateMachine
{
    private readonly ITaskStore _tasks;
    private readonly IWorkerStore _workers;
    private readonly IAssignmentStore _assignments;
    private readonly IEventBus _events;
    private readonly IBruceLogger _logger;

    private static readonly Dictionary<TaskState, TaskState[]> ValidTransitions = new()
    {
        [TaskState.Created] = new[] { TaskState.Reviewed },
        [TaskState.Reviewed] = new[] { TaskState.Assigned, TaskState.Created }, // Can reject back
        [TaskState.Assigned] = new[] { TaskState.Testing, TaskState.Reviewed }, // Can unassign
        [TaskState.Testing] = new[] { TaskState.Done, TaskState.Assigned },     // Can fail back
        [TaskState.Done] = Array.Empty<TaskState>()                              // Terminal
    };

    public TaskStateMachine(
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

    public bool CanTransition(BruceTask task, TaskState newState)
    {
        if (!ValidTransitions.TryGetValue(task.State, out var valid))
            return false;

        if (!valid.Contains(newState))
            return false;

        // Guard: Assigned requires assignee
        if (newState == TaskState.Assigned && string.IsNullOrEmpty(task.AssignedTo))
            return false;

        // Guard: Assignee must exist
        if (newState == TaskState.Assigned && task.AssignedTo != null)
        {
            var worker = ((IStore<Worker>)_workers).Get(task.AssignedTo);
            if (worker == null || !worker.IsActive)
                return false;
        }

        return true;
    }

    public Result Transition(BruceTask task, TaskState newState)
    {
        // Validate transition is allowed
        if (!ValidTransitions.TryGetValue(task.State, out var valid))
        {
            return Result.InvalidState($"Unknown current state: {task.State}");
        }

        if (!valid.Contains(newState))
        {
            return Result.InvalidState(
                $"Invalid transition from {task.State} to {newState}. " +
                $"Valid transitions: {string.Join(", ", valid)}"
            );
        }

        // State-specific guards
        var guardResult = ValidateGuards(task, newState);
        if (guardResult.IsFailure)
        {
            return guardResult;
        }

        var oldState = task.State;
        task.State = newState;

        // Save task
        var saveResult = ((IStore<BruceTask>)_tasks).Save(task);
        if (saveResult.IsFailure)
        {
            return saveResult;
        }

        _events.Publish(new TaskStateChanged(task, oldState, newState));
        _logger.Info($"Task {task.Id} transitioned: {oldState} -> {newState}");

        // Handle side effects
        HandleTransitionSideEffects(task, oldState, newState);

        return Result.Ok();
    }

    private Result ValidateGuards(BruceTask task, TaskState newState)
    {
        switch (newState)
        {
            case TaskState.Assigned:
                if (string.IsNullOrEmpty(task.AssignedTo))
                {
                    return Result.Invalid("Cannot transition to Assigned without an assignee");
                }
                
                var worker = ((IStore<Worker>)_workers).Get(task.AssignedTo);
                if (worker == null)
                {
                    return Result.NotFound($"Assignee {task.AssignedTo} not found");
                }
                if (!worker.IsActive)
                {
                    return Result.Invalid($"Assignee {worker.Name} is not active");
                }
                break;

            case TaskState.Done:
                if (string.IsNullOrEmpty(task.AssignedTo))
                {
                    return Result.Invalid("Cannot complete task without an assignee");
                }
                break;
        }

        return Result.Ok();
    }

    private void HandleTransitionSideEffects(BruceTask task, TaskState oldState, TaskState newState)
    {
        // Handle completion
        if (newState == TaskState.Done)
        {
            var assignment = _assignments.Get(task.Id);
            if (assignment != null)
            {
                var startTime = assignment.ClaimedAt ?? assignment.PushedAt ?? assignment.Created;
                var duration = DateTime.UtcNow - startTime;

                assignment.CompletedAt = DateTime.UtcNow;
                _assignments.Save(assignment);

                var worker = ((IStore<Worker>)_workers).Get(assignment.WorkerId);
                if (worker != null)
                {
                    _events.Publish(new TaskCompleted(task, worker, duration));
                }
            }
        }

        // Handle unassignment (going backward from Assigned or Testing)
        if ((oldState == TaskState.Assigned || oldState == TaskState.Testing) &&
            (newState == TaskState.Reviewed || newState == TaskState.Created))
        {
            var assignment = _assignments.Get(task.Id);
            Worker? previousWorker = null;
            
            if (assignment != null)
            {
                previousWorker = ((IStore<Worker>)_workers).Get(assignment.WorkerId);
                _assignments.Delete(task.Id);
            }

            // Clear assignee
            task.AssignedTo = null;
            ((IStore<BruceTask>)_tasks).Save(task);

            if (previousWorker != null)
            {
                _events.Publish(new TaskUnassigned(task, previousWorker, "State rollback"));
            }
        }
    }

    public TaskState[] GetValidTransitions(BruceTask task)
    {
        if (!ValidTransitions.TryGetValue(task.State, out var valid))
            return Array.Empty<TaskState>();

        return valid.Where(s => CanTransition(task, s)).ToArray();
    }

    /// <summary>
    /// Get a description of the state
    /// </summary>
    public static string GetStateDescription(TaskState state) => state switch
    {
        TaskState.Created => "New task awaiting review",
        TaskState.Reviewed => "Reviewed and ready for assignment",
        TaskState.Assigned => "Assigned to a worker",
        TaskState.Testing => "In testing/verification",
        TaskState.Done => "Completed",
        _ => "Unknown state"
    };
}
