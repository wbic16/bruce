using Bruce.Core.Coordinates;
using Bruce.Core.Enums;

namespace Bruce.Core.Models;

/// <summary>
/// Immutable DTOs for external consumption.
/// Use these when returning data to UI or external systems.
/// </summary>

public record TaskDto(
    string Id,
    string Coord,
    string Title,
    string Description,
    TaskSource Source,
    string? SourceRef,
    TaskState State,
    TaskType Type,
    string? AssignedTo,
    string? AssignedToName,
    DateTime Created,
    DateTime Updated
)
{
    public static TaskDto FromEntity(BruceTask task, string? assignedToName = null) => new(
        task.Id,
        task.Coord.ToShort(),
        task.Title,
        task.Description,
        task.Source,
        task.SourceRef,
        task.State,
        task.Type,
        task.AssignedTo,
        assignedToName,
        task.Created,
        task.Updated
    );
}

public record WorkerDto(
    string Id,
    string Coord,
    string Name,
    WorkerType Type,
    WorkerRole Role,
    string? Substrate,
    bool IsActive,
    int MaxCapacity,
    int MaxAdhoc,
    int MaxPlanned,
    WorkerLoad CurrentLoad
)
{
    public static WorkerDto FromEntity(Worker worker, WorkerLoad? load = null) => new(
        worker.Id,
        worker.Coord.ToShort(),
        worker.Name,
        worker.Type,
        worker.Role,
        worker.Substrate,
        worker.IsActive,
        worker.MaxCapacity,
        worker.MaxAdhoc,
        worker.MaxPlanned,
        load ?? new WorkerLoad()
    );

    public int AvailableCapacity => MaxCapacity - CurrentLoad.Total;
    public bool CanAcceptAdhoc => Role == WorkerRole.Worker 
        ? CurrentLoad.Adhoc < MaxAdhoc 
        : CurrentLoad.Total < MaxCapacity;
    public bool CanAcceptPlanned => Role == WorkerRole.Worker 
        ? CurrentLoad.Planned < MaxPlanned 
        : CurrentLoad.Total < MaxCapacity;
}

public record AssignmentDto(
    string TaskId,
    string WorkerId,
    string WorkerName,
    DateTime? PushedAt,
    DateTime? ClaimedAt,
    DateTime? CompletedAt,
    bool IsPush,
    bool IsComplete
)
{
    public static AssignmentDto FromEntity(Assignment a, string workerName) => new(
        a.TaskId,
        a.WorkerId,
        workerName,
        a.PushedAt,
        a.ClaimedAt,
        a.CompletedAt,
        a.IsPush,
        a.IsComplete
    );
}

public record MessageDto(
    string Id,
    string? TaskId,
    string FromWorkerId,
    string FromWorkerName,
    string? ToWorkerId,
    string? ToWorkerName,
    string Body,
    DateTime Timestamp,
    bool IsBroadcast
)
{
    public static MessageDto FromEntity(Message m, string fromName, string? toName = null) => new(
        m.Id,
        m.TaskId,
        m.FromWorkerId,
        fromName,
        m.ToWorkerId,
        toName,
        m.Body,
        m.Timestamp,
        m.IsBroadcast
    );
}

public record ArtifactDto(
    string Id,
    string TaskId,
    string Name,
    string ContentType,
    int ContentLength,
    DateTime Created
)
{
    public static ArtifactDto FromEntity(Artifact a) => new(
        a.Id,
        a.TaskId,
        a.Name,
        a.ContentType,
        a.Content.Length,
        a.Created
    );
}

/// <summary>
/// Summary view of system state
/// </summary>
public record SystemSummaryDto(
    int TotalTasks,
    int TasksByState_Created,
    int TasksByState_Reviewed,
    int TasksByState_Assigned,
    int TasksByState_Testing,
    int TasksByState_Done,
    int TotalWorkers,
    int ActiveWorkers,
    int HumanWorkers,
    int AiWorkers,
    int UnreadMessages,
    Dictionary<int, (int Used, int Remaining)> AddressSpace
);

/// <summary>
/// Context snapshot DTO
/// </summary>
public record WorkerContextDto(
    string WorkerId,
    string WorkerName,
    WorkerLoad Load,
    IReadOnlyList<TaskDto> AssignedTasks,
    IReadOnlyList<TaskDto> AvailableTasks,
    IReadOnlyList<MessageDto> RecentMessages,
    DateTime AsOf
);
