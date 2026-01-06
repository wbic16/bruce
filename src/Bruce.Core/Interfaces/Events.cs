using Bruce.Core.Enums;
using Bruce.Core.Models;

namespace Bruce.Core.Interfaces;

// === Event Types ===

public abstract record BruceEvent
{
    public string EventId { get; } = Guid.NewGuid().ToString("N")[..12];
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string? CorrelationId { get; init; }
}

public record TaskCreated(BruceTask Task) : BruceEvent;

public record TaskStateChanged(
    BruceTask Task, 
    TaskState OldState, 
    TaskState NewState
) : BruceEvent;

public record TaskAssigned(
    BruceTask Task, 
    Worker Worker, 
    bool Pushed
) : BruceEvent;

public record TaskUnassigned(
    BruceTask Task,
    Worker PreviousWorker,
    string Reason
) : BruceEvent;

public record TaskCompleted(
    BruceTask Task, 
    Worker Worker,
    TimeSpan Duration
) : BruceEvent;

public record WorkerCreated(Worker Worker) : BruceEvent;

public record WorkerUpdated(
    Worker Worker,
    Worker Previous
) : BruceEvent;

public record MessageSent(Message Message) : BruceEvent;

public record ArtifactAttached(
    Artifact Artifact,
    BruceTask Task
) : BruceEvent;

// === Event Bus ===

/// <summary>
/// Synchronous event handler
/// </summary>
public delegate void BruceEventHandler<in T>(T evt) where T : BruceEvent;

/// <summary>
/// Asynchronous event handler
/// </summary>
public delegate Task BruceAsyncEventHandler<in T>(T evt, CancellationToken ct) where T : BruceEvent;

/// <summary>
/// Event bus supporting both sync and async handlers
/// </summary>
public interface IEventBus
{
    /// <summary>Publish event to all subscribers</summary>
    void Publish<T>(T evt) where T : BruceEvent;
    
    /// <summary>Publish event and wait for async handlers</summary>
    Task PublishAsync<T>(T evt, CancellationToken ct = default) where T : BruceEvent;

    /// <summary>Subscribe synchronous handler</summary>
    IDisposable Subscribe<T>(BruceEventHandler<T> handler) where T : BruceEvent;
    
    /// <summary>Subscribe asynchronous handler</summary>
    IDisposable Subscribe<T>(BruceAsyncEventHandler<T> handler) where T : BruceEvent;

    /// <summary>Legacy subscribe (returns void, use Subscribe for IDisposable)</summary>
    void SubscribeLegacy<T>(Action<T> handler) where T : BruceEvent;
}

/// <summary>
/// Subscription handle for unsubscribing
/// </summary>
public interface ISubscription : IDisposable
{
    bool IsActive { get; }
}
