using Bruce.Core.Coordinates;
using Bruce.Core.Enums;

namespace Bruce.Core.Models;

/// <summary>
/// Base class for all entities with common fields
/// </summary>
public abstract class EntityBase
{
    public string Id { get; set; } = string.Empty;
    public BruceCoord Coord { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime Updated { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Optimistic concurrency version. Incremented on each save.
    /// </summary>
    public int Version { get; set; } = 1;
}

public class BruceTask : EntityBase
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskSource Source { get; set; }
    public string? SourceRef { get; set; }  // External ID from Teams/Email/Helix
    public TaskState State { get; set; } = TaskState.Created;
    public TaskType Type { get; set; } = TaskType.Adhoc;
    public string? AssignedTo { get; set; }  // Worker.Id
    
    /// <summary>
    /// Create a deep clone for safe mutation
    /// </summary>
    public BruceTask Clone() => new()
    {
        Id = Id,
        Coord = Coord,
        Title = Title,
        Description = Description,
        Source = Source,
        SourceRef = SourceRef,
        State = State,
        Type = Type,
        AssignedTo = AssignedTo,
        Created = Created,
        Updated = Updated,
        Version = Version
    };
}

public class Worker : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public WorkerType Type { get; set; }
    public WorkerRole Role { get; set; }
    public string? Substrate { get; set; }  // For AI: claude, gpt, grok, gemini, etc.
    public BruceCoord? ViewCoord { get; set; }  // Current context snapshot location
    public bool IsActive { get; set; } = true;

    public int MaxCapacity => Role switch
    {
        WorkerRole.Worker => 4,    // 2+2
        WorkerRole.Manager => 24,
        WorkerRole.Director => 200,
        _ => 4
    };

    public int MaxAdhoc => Role == WorkerRole.Worker ? 2 : MaxCapacity;
    public int MaxPlanned => Role == WorkerRole.Worker ? 2 : MaxCapacity;

    public Worker Clone() => new()
    {
        Id = Id,
        Coord = Coord,
        Name = Name,
        Type = Type,
        Role = Role,
        Substrate = Substrate,
        ViewCoord = ViewCoord,
        IsActive = IsActive,
        Created = Created,
        Updated = Updated,
        Version = Version
    };
}

public class Assignment : EntityBase
{
    public string TaskId { get; set; } = string.Empty;
    public string WorkerId { get; set; } = string.Empty;
    public DateTime? PushedAt { get; set; }   // If coordinator assigned
    public DateTime? ClaimedAt { get; set; }  // If worker pulled
    public DateTime? CompletedAt { get; set; }
    
    public bool IsPush => PushedAt.HasValue;
    public bool IsClaim => ClaimedAt.HasValue;
    public bool IsComplete => CompletedAt.HasValue;
}

public class Artifact : EntityBase
{
    public string TaskId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/plain";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public class Message : EntityBase
{
    public string? TaskId { get; set; }  // Optional task association
    public string FromWorkerId { get; set; } = string.Empty;
    public string? ToWorkerId { get; set; }  // Null = broadcast
    public string Body { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public bool IsBroadcast => ToWorkerId == null;
}

/// <summary>
/// Snapshot of system state for a worker's current view
/// </summary>
public class WorkerContext
{
    public string WorkerId { get; set; } = string.Empty;
    public List<BruceTask> AssignedTasks { get; set; } = new();
    public List<BruceTask> AvailableTasks { get; set; } = new();
    public List<Message> RecentMessages { get; set; } = new();
    public WorkerLoad Load { get; set; } = new();
    public DateTime AsOf { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Worker load information
/// </summary>
public record WorkerLoad(int Adhoc = 0, int Planned = 0)
{
    public int Total => Adhoc + Planned;
}
