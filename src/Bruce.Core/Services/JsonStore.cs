using System.Text.Json;
using Bruce.Core.Configuration;
using Bruce.Core.Coordinates;
using Bruce.Core.Enums;
using Bruce.Core.Interfaces;
using Bruce.Core.Models;
using Bruce.Core.Primitives;

namespace Bruce.Core.Services;

/// <summary>
/// JSON file-based persistence with atomic writes and optimistic concurrency.
/// </summary>
public class JsonStore : ITaskStore, IWorkerStore, IAssignmentStore, IArtifactStore, IMessageStore
{
    private readonly string _basePath;
    private readonly bool _atomicWrites;
    private readonly IBruceLogger _logger;
    private readonly CoordAllocator _allocator = new();
    private readonly ReaderWriterLockSlim _lock = new();

    private Dictionary<string, BruceTask> _tasks = new();
    private Dictionary<string, Worker> _workers = new();
    private Dictionary<string, Assignment> _assignments = new();
    private Dictionary<string, Artifact> _artifacts = new();
    private Dictionary<string, Message> _messages = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonStore(BruceConfig config, IBruceLogger? logger = null)
    {
        _basePath = config.DataPath;
        _atomicWrites = config.AtomicWrites;
        _logger = logger ?? NullLogger.Instance;
        
        Directory.CreateDirectory(_basePath);
        Load();
    }

    public JsonStore(string basePath = "bruce_data") 
        : this(new BruceConfig { DataPath = basePath }) { }

    public CoordAllocator Allocator => _allocator;

    private void Load()
    {
        _lock.EnterWriteLock();
        try
        {
            _tasks = LoadFile<Dictionary<string, BruceTask>>("tasks.json") ?? new();
            _workers = LoadFile<Dictionary<string, Worker>>("workers.json") ?? new();
            _assignments = LoadFile<Dictionary<string, Assignment>>("assignments.json") ?? new();
            _artifacts = LoadFile<Dictionary<string, Artifact>>("artifacts.json") ?? new();
            _messages = LoadFile<Dictionary<string, Message>>("messages.json") ?? new();

            // Rebuild allocator high water marks
            SetHighWaterFromCoords(_tasks.Values.Select(t => t.Coord), BruceCoord.Tasks);
            SetHighWaterFromCoords(_workers.Values.Select(w => w.Coord), BruceCoord.Users);
            SetHighWaterFromCoords(_artifacts.Values.Select(a => a.Coord), BruceCoord.Artifacts);
            SetHighWaterFromCoords(_messages.Values.Select(m => m.Coord), BruceCoord.Messages);

            _logger.Info($"Loaded {_tasks.Count} tasks, {_workers.Count} workers, {_messages.Count} messages");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void SetHighWaterFromCoords(IEnumerable<BruceCoord> coords, int entityType)
    {
        var maxPos = coords
            .Where(c => c.EntityType == entityType)
            .Select(c => c.LinearPosition)
            .DefaultIfEmpty(-1)
            .Max();

        if (maxPos >= 0)
        {
            _allocator.SetHighWater(entityType, maxPos);
        }
    }

    private T? LoadFile<T>(string filename) where T : class
    {
        var path = Path.Combine(_basePath, filename);
        if (!File.Exists(path)) return null;
        
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load {filename}", ex);
            return null;
        }
    }

    private void SaveFile<T>(string filename, T data)
    {
        var path = Path.Combine(_basePath, filename);
        var json = JsonSerializer.Serialize(data, JsonOpts);

        if (_atomicWrites)
        {
            // Write to temp file then atomic rename
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
        }
        else
        {
            File.WriteAllText(path, json);
        }
    }

    // === ITaskStore ===

    public BruceTask? Get(string id)
    {
        _lock.EnterReadLock();
        try
        {
            return _tasks.GetValueOrDefault(id)?.Clone();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    Result IStore<BruceTask>.Save(BruceTask task)
    {
        _lock.EnterWriteLock();
        try
        {
            // Optimistic concurrency check
            if (_tasks.TryGetValue(task.Id, out var existing))
            {
                if (existing.Version != task.Version)
                {
                    return Result.Fail(
                        $"Concurrency conflict: task {task.Id} was modified (expected v{task.Version}, found v{existing.Version})",
                        ResultErrorCode.ConcurrencyConflict
                    );
                }
            }

            task.Updated = DateTime.UtcNow;
            task.Version++;
            _tasks[task.Id] = task.Clone();
            SaveFile("tasks.json", _tasks);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save task {task.Id}", ex);
            return Result.Fail($"Persistence failed: {ex.Message}", ResultErrorCode.PersistenceFailed);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    Result IStore<BruceTask>.Delete(string id)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_tasks.Remove(id))
                return Result.NotFound($"Task {id} not found");
            
            SaveFile("tasks.json", _tasks);
            return Result.Ok();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public IEnumerable<BruceTask> GetAll()
    {
        _lock.EnterReadLock();
        try
        {
            return _tasks.Values.Select(t => t.Clone()).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IEnumerable<BruceTask> Query(Func<BruceTask, bool> predicate)
    {
        _lock.EnterReadLock();
        try
        {
            return _tasks.Values.Where(predicate).Select(t => t.Clone()).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IEnumerable<BruceTask> GetByState(TaskState state)
    {
        return Query(t => t.State == state);
    }

    public IEnumerable<BruceTask> GetByAssignee(string workerId)
    {
        return Query(t => t.AssignedTo == workerId);
    }

    public IEnumerable<BruceTask> GetUnassigned()
    {
        return Query(t => t.AssignedTo == null && t.State == TaskState.Reviewed);
    }

    // === IWorkerStore ===

    Worker? IStore<Worker>.Get(string id)
    {
        _lock.EnterReadLock();
        try
        {
            return _workers.GetValueOrDefault(id)?.Clone();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    Result IStore<Worker>.Save(Worker worker)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_workers.TryGetValue(worker.Id, out var existing))
            {
                if (existing.Version != worker.Version)
                {
                    return Result.Fail(
                        $"Concurrency conflict: worker {worker.Id} was modified",
                        ResultErrorCode.ConcurrencyConflict
                    );
                }
            }

            worker.Updated = DateTime.UtcNow;
            worker.Version++;
            _workers[worker.Id] = worker.Clone();
            SaveFile("workers.json", _workers);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save worker {worker.Id}", ex);
            return Result.Fail($"Persistence failed: {ex.Message}", ResultErrorCode.PersistenceFailed);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    Result IStore<Worker>.Delete(string id)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_workers.Remove(id))
                return Result.NotFound($"Worker {id} not found");
            
            SaveFile("workers.json", _workers);
            return Result.Ok();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    IEnumerable<Worker> IStore<Worker>.GetAll()
    {
        _lock.EnterReadLock();
        try
        {
            return _workers.Values.Select(w => w.Clone()).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IEnumerable<Worker> GetByType(WorkerType type)
    {
        _lock.EnterReadLock();
        try
        {
            return _workers.Values.Where(w => w.Type == type).Select(w => w.Clone()).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IEnumerable<Worker> GetByRole(WorkerRole role)
    {
        _lock.EnterReadLock();
        try
        {
            return _workers.Values.Where(w => w.Role == role).Select(w => w.Clone()).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IEnumerable<Worker> GetActive()
    {
        _lock.EnterReadLock();
        try
        {
            return _workers.Values.Where(w => w.IsActive).Select(w => w.Clone()).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    // === IAssignmentStore ===

    Assignment? IAssignmentStore.Get(string taskId)
    {
        _lock.EnterReadLock();
        try
        {
            return _assignments.GetValueOrDefault(taskId);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    Result IAssignmentStore.Save(Assignment a)
    {
        _lock.EnterWriteLock();
        try
        {
            a.Updated = DateTime.UtcNow;
            _assignments[a.TaskId] = a;
            SaveFile("assignments.json", _assignments);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Persistence failed: {ex.Message}", ResultErrorCode.PersistenceFailed);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    Result IAssignmentStore.Delete(string taskId)
    {
        _lock.EnterWriteLock();
        try
        {
            _assignments.Remove(taskId);
            SaveFile("assignments.json", _assignments);
            return Result.Ok();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    IEnumerable<Assignment> IAssignmentStore.GetByWorker(string workerId)
    {
        _lock.EnterReadLock();
        try
        {
            return _assignments.Values.Where(a => a.WorkerId == workerId).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    IEnumerable<Assignment> IAssignmentStore.GetActive()
    {
        _lock.EnterReadLock();
        try
        {
            return _assignments.Values.Where(a => a.CompletedAt == null).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    IEnumerable<Assignment> IAssignmentStore.GetCompleted(DateTime since)
    {
        _lock.EnterReadLock();
        try
        {
            return _assignments.Values
                .Where(a => a.CompletedAt.HasValue && a.CompletedAt.Value >= since)
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    // === IArtifactStore ===

    Artifact? IStore<Artifact>.Get(string id)
    {
        _lock.EnterReadLock();
        try
        {
            return _artifacts.GetValueOrDefault(id);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    Result IStore<Artifact>.Save(Artifact a)
    {
        _lock.EnterWriteLock();
        try
        {
            a.Updated = DateTime.UtcNow;
            _artifacts[a.Id] = a;
            SaveFile("artifacts.json", _artifacts);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Persistence failed: {ex.Message}", ResultErrorCode.PersistenceFailed);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    Result IStore<Artifact>.Delete(string id)
    {
        _lock.EnterWriteLock();
        try
        {
            _artifacts.Remove(id);
            SaveFile("artifacts.json", _artifacts);
            return Result.Ok();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    IEnumerable<Artifact> IStore<Artifact>.GetAll()
    {
        _lock.EnterReadLock();
        try
        {
            return _artifacts.Values.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    IEnumerable<Artifact> IArtifactStore.GetByTask(string taskId)
    {
        _lock.EnterReadLock();
        try
        {
            return _artifacts.Values.Where(a => a.TaskId == taskId).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    // === IMessageStore ===

    Message? IMessageStore.Get(string id)
    {
        _lock.EnterReadLock();
        try
        {
            return _messages.GetValueOrDefault(id);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    Result IMessageStore.Save(Message m)
    {
        _lock.EnterWriteLock();
        try
        {
            m.Updated = DateTime.UtcNow;
            _messages[m.Id] = m;
            SaveFile("messages.json", _messages);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Persistence failed: {ex.Message}", ResultErrorCode.PersistenceFailed);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    IEnumerable<Message> IMessageStore.GetByTask(string taskId)
    {
        _lock.EnterReadLock();
        try
        {
            return _messages.Values
                .Where(m => m.TaskId == taskId)
                .OrderBy(m => m.Timestamp)
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    IEnumerable<Message> IMessageStore.GetForWorker(string workerId, DateTime since)
    {
        _lock.EnterReadLock();
        try
        {
            return _messages.Values
                .Where(m => (m.ToWorkerId == workerId || m.ToWorkerId == null) && m.Timestamp > since)
                .OrderBy(m => m.Timestamp)
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    IEnumerable<Message> IMessageStore.GetBroadcasts(DateTime since)
    {
        _lock.EnterReadLock();
        try
        {
            return _messages.Values
                .Where(m => m.ToWorkerId == null && m.Timestamp > since)
                .OrderBy(m => m.Timestamp)
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    IEnumerable<Message> IMessageStore.GetRecent(int count)
    {
        _lock.EnterReadLock();
        try
        {
            return _messages.Values
                .OrderByDescending(m => m.Timestamp)
                .Take(count)
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}
