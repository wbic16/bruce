using Bruce.Core.Configuration;
using Bruce.Core.Interfaces;

namespace Bruce.Core.Services;

/// <summary>
/// In-memory event bus supporting both sync and async handlers.
/// Thread-safe with proper error isolation.
/// </summary>
public class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<HandlerEntry>> _handlers = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly IBruceLogger _logger;

    public EventBus(IBruceLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public void Publish<T>(T evt) where T : BruceEvent
    {
        var handlers = GetHandlers<T>();
        if (handlers.Count == 0) return;

        _logger.Debug($"Publishing {typeof(T).Name} to {handlers.Count} handlers");

        foreach (var entry in handlers)
        {
            try
            {
                if (entry.SyncHandler != null)
                {
                    ((BruceEventHandler<T>)entry.SyncHandler)(evt);
                }
                else if (entry.AsyncHandler != null)
                {
                    // Fire and forget for async handlers in sync publish
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ((BruceAsyncEventHandler<T>)entry.AsyncHandler)(evt, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Async handler error for {typeof(T).Name}", ex);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Handler error for {typeof(T).Name}", ex);
                // Continue to next handler - don't let one failure stop others
            }
        }
    }

    public async Task PublishAsync<T>(T evt, CancellationToken ct = default) where T : BruceEvent
    {
        var handlers = GetHandlers<T>();
        if (handlers.Count == 0) return;

        _logger.Debug($"Publishing async {typeof(T).Name} to {handlers.Count} handlers");

        var tasks = new List<Task>();

        foreach (var entry in handlers)
        {
            if (entry.SyncHandler != null)
            {
                try
                {
                    ((BruceEventHandler<T>)entry.SyncHandler)(evt);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Sync handler error for {typeof(T).Name}", ex);
                }
            }
            else if (entry.AsyncHandler != null)
            {
                tasks.Add(InvokeAsync(entry.AsyncHandler, evt, ct));
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    private async Task InvokeAsync<T>(Delegate handler, T evt, CancellationToken ct) where T : BruceEvent
    {
        try
        {
            await ((BruceAsyncEventHandler<T>)handler)(evt, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected cancellation
        }
        catch (Exception ex)
        {
            _logger.Error($"Async handler error for {typeof(T).Name}", ex);
        }
    }

    public IDisposable Subscribe<T>(BruceEventHandler<T> handler) where T : BruceEvent
    {
        var entry = new HandlerEntry(handler, null);
        AddHandler<T>(entry);
        return new Subscription(() => RemoveHandler<T>(entry));
    }

    public IDisposable Subscribe<T>(BruceAsyncEventHandler<T> handler) where T : BruceEvent
    {
        var entry = new HandlerEntry(null, handler);
        AddHandler<T>(entry);
        return new Subscription(() => RemoveHandler<T>(entry));
    }

    public void SubscribeLegacy<T>(Action<T> handler) where T : BruceEvent
    {
        var entry = new HandlerEntry(new BruceEventHandler<T>(handler), null);
        AddHandler<T>(entry);
    }

    private void AddHandler<T>(HandlerEntry entry)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                list = new List<HandlerEntry>();
                _handlers[typeof(T)] = list;
            }
            list.Add(entry);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void RemoveHandler<T>(HandlerEntry entry)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
            {
                list.Remove(entry);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private List<HandlerEntry> GetHandlers<T>()
    {
        _lock.EnterReadLock();
        try
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
            {
                return list.ToList(); // Return snapshot
            }
            return new List<HandlerEntry>();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private record HandlerEntry(Delegate? SyncHandler, Delegate? AsyncHandler);

    private class Subscription : IDisposable
    {
        private Action? _unsubscribe;
        private bool _disposed;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _unsubscribe?.Invoke();
            _unsubscribe = null;
        }
    }
}
