// ===========================================================================================================
// Bruce.Core: PhextStore
// Phext-based persistence layer for hybrid human-AI team coordination
//
// Sprint 4, Day 741 - Replacing JSON with native phext storage
// Copyright (c) 2026 Will Bickford
// ===========================================================================================================

using System.Text;
using Bruce.Core.Configuration;
using Bruce.Core.Coordinates;
using Bruce.Core.Enums;
using Bruce.Core.Interfaces;
using Bruce.Core.Models;
using Bruce.Core.Primitives;
using Phext;

namespace Bruce.Core.Services;

/// <summary>
/// Phext-based persistence with coordinate-addressed storage.
/// 
/// Storage Layout (using phext coordinate space):
/// ─────────────────────────────────────────────
/// Library 1: System Data
///   Shelf 1: Entities
///     Series 1: Workers     (scroll per worker)
///     Series 2: Tasks       (scroll per task)
///     Series 3: Assignments (scroll per assignment)
///     Series 4: Artifacts   (scroll per artifact, binary base64 encoded)
///     Series 5: Messages    (scroll per message)
///   Shelf 2: Indexes (for fast lookup)
///     Series 1: ID → Coordinate mappings
///     Series 2: State indexes
///   Shelf 3: Metadata
///     Series 1: Version info
///     Series 2: High water marks
/// 
/// Each entity is serialized as a simple key=value format within its scroll.
/// </summary>
public class PhextStore : ITaskStore, IWorkerStore, IAssignmentStore, IArtifactStore, IMessageStore
{
    private readonly string _phextPath;
    private readonly bool _atomicWrites;
    private readonly IBruceLogger _logger;
    private readonly CoordAllocator _allocator = new();
    private readonly ReaderWriterLockSlim _lock = new();

    // In-memory caches (loaded from phext on startup)
    private Dictionary<string, BruceTask> _tasks = new();
    private Dictionary<string, Worker> _workers = new();
    private Dictionary<string, Assignment> _assignments = new();
    private Dictionary<string, Artifact> _artifacts = new();
    private Dictionary<string, Message> _messages = new();

    // Phext coordinate mappings (ID → phext coordinate)
    private Dictionary<string, Coordinate> _taskCoords = new();
    private Dictionary<string, Coordinate> _workerCoords = new();
    private Dictionary<string, Coordinate> _assignmentCoords = new();
    private Dictionary<string, Coordinate> _artifactCoords = new();
    private Dictionary<string, Coordinate> _messageCoords = new();

    // Series assignments for each entity type
    private static class PhextSeries
    {
        public const int Workers = 1;
        public const int Tasks = 2;
        public const int Assignments = 3;
        public const int Artifacts = 4;
        public const int Messages = 5;
    }

    // Scroll counters per series
    private readonly Dictionary<int, int> _scrollCounters = new()
    {
        [PhextSeries.Workers] = 1,
        [PhextSeries.Tasks] = 1,
        [PhextSeries.Assignments] = 1,
        [PhextSeries.Artifacts] = 1,
        [PhextSeries.Messages] = 1
    };

    // The raw phext document
    private string _phext = "";

    public PhextStore(BruceConfig config, IBruceLogger? logger = null)
    {
        _phextPath = Path.Combine(config.DataPath, "bruce.phext");
        _atomicWrites = config.AtomicWrites;
        _logger = logger ?? NullLogger.Instance;
        
        Directory.CreateDirectory(config.DataPath);
        Load();
    }

    public PhextStore(string basePath = "bruce_data")
        : this(new BruceConfig { DataPath = basePath }) { }

    public CoordAllocator Allocator => _allocator;

    #region Phext Coordinate Helpers

    /// <summary>
    /// Creates a phext coordinate for an entity in the given series
    /// </summary>
    private Coordinate AllocateCoord(int series)
    {
        var scroll = _scrollCounters[series]++;
        // Coordinates: 1.1.{series}/1.1.1/1.1.{scroll}
        return new Coordinate(1, 1, series, 1, 1, 1, 1, 1, scroll);
    }

    /// <summary>
    /// Serializes an entity to phext scroll format (key=value lines)
    /// </summary>
    private static string SerializeToScroll<T>(T entity) where T : EntityBase
    {
        var sb = new StringBuilder();
        
        // Common fields
        sb.AppendLine($"id={entity.Id}");
        sb.AppendLine($"coord={entity.Coord}");
        sb.AppendLine($"created={entity.Created:O}");
        sb.AppendLine($"updated={entity.Updated:O}");
        sb.AppendLine($"version={entity.Version}");

        switch (entity)
        {
            case BruceTask task:
                sb.AppendLine($"title={EscapeValue(task.Title)}");
                sb.AppendLine($"description={EscapeValue(task.Description)}");
                sb.AppendLine($"source={task.Source}");
                sb.AppendLine($"sourceRef={task.SourceRef ?? ""}");
                sb.AppendLine($"state={task.State}");
                sb.AppendLine($"type={task.Type}");
                sb.AppendLine($"assignedTo={task.AssignedTo ?? ""}");
                break;

            case Worker worker:
                sb.AppendLine($"name={EscapeValue(worker.Name)}");
                sb.AppendLine($"type={worker.Type}");
                sb.AppendLine($"role={worker.Role}");
                sb.AppendLine($"substrate={worker.Substrate ?? ""}");
                sb.AppendLine($"viewCoord={worker.ViewCoord?.ToString() ?? ""}");
                sb.AppendLine($"isActive={worker.IsActive}");
                break;

            case Assignment assignment:
                sb.AppendLine($"taskId={assignment.TaskId}");
                sb.AppendLine($"workerId={assignment.WorkerId}");
                sb.AppendLine($"pushedAt={assignment.PushedAt?.ToString("O") ?? ""}");
                sb.AppendLine($"claimedAt={assignment.ClaimedAt?.ToString("O") ?? ""}");
                sb.AppendLine($"completedAt={assignment.CompletedAt?.ToString("O") ?? ""}");
                break;

            case Artifact artifact:
                sb.AppendLine($"taskId={artifact.TaskId}");
                sb.AppendLine($"name={EscapeValue(artifact.Name)}");
                sb.AppendLine($"contentType={artifact.ContentType}");
                sb.AppendLine($"content={Convert.ToBase64String(artifact.Content)}");
                break;

            case Message message:
                sb.AppendLine($"taskId={message.TaskId ?? ""}");
                sb.AppendLine($"fromWorkerId={message.FromWorkerId}");
                sb.AppendLine($"toWorkerId={message.ToWorkerId ?? ""}");
                sb.AppendLine($"body={EscapeValue(message.Body)}");
                sb.AppendLine($"timestamp={message.Timestamp:O}");
                break;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Deserializes a phext scroll to an entity
    /// </summary>
    private static T? DeserializeFromScroll<T>(string scroll) where T : EntityBase, new()
    {
        if (string.IsNullOrWhiteSpace(scroll))
            return null;

        var dict = new Dictionary<string, string>();
        foreach (var line in scroll.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = line.IndexOf('=');
            if (idx > 0)
            {
                var key = line[..idx].Trim();
                var value = UnescapeValue(line[(idx + 1)..].Trim());
                dict[key] = value;
            }
        }

        if (!dict.TryGetValue("id", out var id) || string.IsNullOrEmpty(id))
            return null;

        var entity = new T
        {
            Id = id,
            Coord = BruceCoord.TryParse(dict.GetValueOrDefault("coord"), out var coord) ? coord : default,
            Created = DateTime.TryParse(dict.GetValueOrDefault("created"), out var created) ? created : DateTime.UtcNow,
            Updated = DateTime.TryParse(dict.GetValueOrDefault("updated"), out var updated) ? updated : DateTime.UtcNow,
            Version = int.TryParse(dict.GetValueOrDefault("version"), out var version) ? version : 1
        };

        switch (entity)
        {
            case BruceTask task:
                task.Title = dict.GetValueOrDefault("title", "");
                task.Description = dict.GetValueOrDefault("description", "");
                task.Source = Enum.TryParse<TaskSource>(dict.GetValueOrDefault("source"), out var source) ? source : TaskSource.Manual;
                task.SourceRef = NullIfEmpty(dict.GetValueOrDefault("sourceRef"));
                task.State = Enum.TryParse<TaskState>(dict.GetValueOrDefault("state"), out var state) ? state : TaskState.Created;
                task.Type = Enum.TryParse<TaskType>(dict.GetValueOrDefault("type"), out var taskType) ? taskType : TaskType.Adhoc;
                task.AssignedTo = NullIfEmpty(dict.GetValueOrDefault("assignedTo"));
                break;

            case Worker worker:
                worker.Name = dict.GetValueOrDefault("name", "");
                worker.Type = Enum.TryParse<WorkerType>(dict.GetValueOrDefault("type"), out var workerType) ? workerType : WorkerType.Human;
                worker.Role = Enum.TryParse<WorkerRole>(dict.GetValueOrDefault("role"), out var role) ? role : WorkerRole.Worker;
                worker.Substrate = NullIfEmpty(dict.GetValueOrDefault("substrate"));
                worker.ViewCoord = BruceCoord.TryParse(dict.GetValueOrDefault("viewCoord"), out var viewCoord) ? viewCoord : null;
                worker.IsActive = bool.TryParse(dict.GetValueOrDefault("isActive"), out var isActive) && isActive;
                break;

            case Assignment assignment:
                assignment.TaskId = dict.GetValueOrDefault("taskId", "");
                assignment.WorkerId = dict.GetValueOrDefault("workerId", "");
                assignment.PushedAt = DateTime.TryParse(dict.GetValueOrDefault("pushedAt"), out var pushed) ? pushed : null;
                assignment.ClaimedAt = DateTime.TryParse(dict.GetValueOrDefault("claimedAt"), out var claimed) ? claimed : null;
                assignment.CompletedAt = DateTime.TryParse(dict.GetValueOrDefault("completedAt"), out var completed) ? completed : null;
                break;

            case Artifact artifact:
                artifact.TaskId = dict.GetValueOrDefault("taskId", "");
                artifact.Name = dict.GetValueOrDefault("name", "");
                artifact.ContentType = dict.GetValueOrDefault("contentType", "text/plain");
                var b64 = dict.GetValueOrDefault("content", "");
                artifact.Content = string.IsNullOrEmpty(b64) ? Array.Empty<byte>() : Convert.FromBase64String(b64);
                break;

            case Message message:
                message.TaskId = NullIfEmpty(dict.GetValueOrDefault("taskId"));
                message.FromWorkerId = dict.GetValueOrDefault("fromWorkerId", "");
                message.ToWorkerId = NullIfEmpty(dict.GetValueOrDefault("toWorkerId"));
                message.Body = dict.GetValueOrDefault("body", "");
                message.Timestamp = DateTime.TryParse(dict.GetValueOrDefault("timestamp"), out var timestamp) ? timestamp : DateTime.UtcNow;
                break;
        }

        return entity;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string EscapeValue(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }

    private static string UnescapeValue(string value)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                switch (value[i + 1])
                {
                    case 'n': sb.Append('\n'); i++; break;
                    case 'r': sb.Append('\r'); i++; break;
                    case '\\': sb.Append('\\'); i++; break;
                    default: sb.Append(value[i]); break;
                }
            }
            else
            {
                sb.Append(value[i]);
            }
        }
        return sb.ToString();
    }

    #endregion

    #region Load/Save

    private void Load()
    {
        _lock.EnterWriteLock();
        try
        {
            if (File.Exists(_phextPath))
            {
                _phext = File.ReadAllText(_phextPath);
                LoadEntitiesFromPhext();
            }
            else
            {
                // Initialize empty phext with header
                _phext = "bruce.phext v0.4.0\n";
                SavePhextToDisk();
            }

            // Rebuild allocator high water marks
            SetHighWaterFromCoords(_tasks.Values.Select(t => t.Coord), BruceCoord.Tasks);
            SetHighWaterFromCoords(_workers.Values.Select(w => w.Coord), BruceCoord.Users);
            SetHighWaterFromCoords(_artifacts.Values.Select(a => a.Coord), BruceCoord.Artifacts);
            SetHighWaterFromCoords(_messages.Values.Select(m => m.Coord), BruceCoord.Messages);

            _logger.Info($"PhextStore loaded: {_tasks.Count} tasks, {_workers.Count} workers, {_messages.Count} messages");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void LoadEntitiesFromPhext()
    {
        // Parse phext and load all entities from their series coordinates
        var phokens = PhextEngine.Phokenize(_phext);

        foreach (var phoken in phokens)
        {
            var coord = phoken.Coord;
            var scroll = phoken.Scroll;

            // Skip header/metadata scrolls
            if (coord.Series < 1 || coord.Series > 5)
                continue;

            // Update scroll counter
            if (coord.Scroll >= _scrollCounters[coord.Series])
                _scrollCounters[coord.Series] = coord.Scroll + 1;

            switch (coord.Series)
            {
                case PhextSeries.Workers:
                    var worker = DeserializeFromScroll<Worker>(scroll);
                    if (worker != null)
                    {
                        _workers[worker.Id] = worker;
                        _workerCoords[worker.Id] = coord;
                    }
                    break;

                case PhextSeries.Tasks:
                    var task = DeserializeFromScroll<BruceTask>(scroll);
                    if (task != null)
                    {
                        _tasks[task.Id] = task;
                        _taskCoords[task.Id] = coord;
                    }
                    break;

                case PhextSeries.Assignments:
                    var assignment = DeserializeFromScroll<Assignment>(scroll);
                    if (assignment != null)
                    {
                        _assignments[assignment.TaskId] = assignment;
                        _assignmentCoords[assignment.TaskId] = coord;
                    }
                    break;

                case PhextSeries.Artifacts:
                    var artifact = DeserializeFromScroll<Artifact>(scroll);
                    if (artifact != null)
                    {
                        _artifacts[artifact.Id] = artifact;
                        _artifactCoords[artifact.Id] = coord;
                    }
                    break;

                case PhextSeries.Messages:
                    var message = DeserializeFromScroll<Message>(scroll);
                    if (message != null)
                    {
                        _messages[message.Id] = message;
                        _messageCoords[message.Id] = coord;
                    }
                    break;
            }
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

    private void SavePhextToDisk()
    {
        if (_atomicWrites)
        {
            var tempPath = _phextPath + ".tmp";
            File.WriteAllText(tempPath, _phext);
            File.Move(tempPath, _phextPath, overwrite: true);
        }
        else
        {
            File.WriteAllText(_phextPath, _phext);
        }
    }

    private void RebuildPhext()
    {
        // Rebuild the entire phext document from in-memory state
        var sb = new StringBuilder();
        sb.AppendLine("bruce.phext v0.4.0");

        // Build all entities in order by their phext coordinates
        var allItems = new List<(Coordinate coord, string scroll)>();

        foreach (var (id, worker) in _workers)
        {
            if (_workerCoords.TryGetValue(id, out var coord))
                allItems.Add((coord, SerializeToScroll(worker)));
        }

        foreach (var (id, task) in _tasks)
        {
            if (_taskCoords.TryGetValue(id, out var coord))
                allItems.Add((coord, SerializeToScroll(task)));
        }

        foreach (var (taskId, assignment) in _assignments)
        {
            if (_assignmentCoords.TryGetValue(taskId, out var coord))
                allItems.Add((coord, SerializeToScroll(assignment)));
        }

        foreach (var (id, artifact) in _artifacts)
        {
            if (_artifactCoords.TryGetValue(id, out var coord))
                allItems.Add((coord, SerializeToScroll(artifact)));
        }

        foreach (var (id, message) in _messages)
        {
            if (_messageCoords.TryGetValue(id, out var coord))
                allItems.Add((coord, SerializeToScroll(message)));
        }

        // Sort by coordinate and rebuild using PhextEngine
        _phext = sb.ToString();
        foreach (var (coord, scroll) in allItems.OrderBy(x => x.coord))
        {
            _phext = PhextEngine.Replace(_phext, coord, scroll);
        }

        SavePhextToDisk();
    }

    #endregion

    #region ITaskStore

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
            else
            {
                // New task - allocate coordinate
                _taskCoords[task.Id] = AllocateCoord(PhextSeries.Tasks);
            }

            task.Updated = DateTime.UtcNow;
            task.Version++;
            _tasks[task.Id] = task.Clone();

            // Update phext document
            var coord = _taskCoords[task.Id];
            _phext = PhextEngine.Replace(_phext, coord, SerializeToScroll(task));
            SavePhextToDisk();

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

            if (_taskCoords.TryGetValue(id, out var coord))
            {
                _phext = PhextEngine.Remove(_phext, coord);
                _taskCoords.Remove(id);
            }

            SavePhextToDisk();
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

    public IEnumerable<BruceTask> GetByState(TaskState state) => Query(t => t.State == state);
    public IEnumerable<BruceTask> GetByAssignee(string workerId) => Query(t => t.AssignedTo == workerId);
    public IEnumerable<BruceTask> GetUnassigned() => Query(t => t.AssignedTo == null && t.State == TaskState.Reviewed);

    #endregion

    #region IWorkerStore

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
            else
            {
                _workerCoords[worker.Id] = AllocateCoord(PhextSeries.Workers);
            }

            worker.Updated = DateTime.UtcNow;
            worker.Version++;
            _workers[worker.Id] = worker.Clone();

            var coord = _workerCoords[worker.Id];
            _phext = PhextEngine.Replace(_phext, coord, SerializeToScroll(worker));
            SavePhextToDisk();

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

            if (_workerCoords.TryGetValue(id, out var coord))
            {
                _phext = PhextEngine.Remove(_phext, coord);
                _workerCoords.Remove(id);
            }

            SavePhextToDisk();
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

    #endregion

    #region IAssignmentStore

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
            if (!_assignmentCoords.ContainsKey(a.TaskId))
            {
                _assignmentCoords[a.TaskId] = AllocateCoord(PhextSeries.Assignments);
            }

            a.Updated = DateTime.UtcNow;
            _assignments[a.TaskId] = a;

            var coord = _assignmentCoords[a.TaskId];
            _phext = PhextEngine.Replace(_phext, coord, SerializeToScroll(a));
            SavePhextToDisk();

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

            if (_assignmentCoords.TryGetValue(taskId, out var coord))
            {
                _phext = PhextEngine.Remove(_phext, coord);
                _assignmentCoords.Remove(taskId);
            }

            SavePhextToDisk();
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

    #endregion

    #region IArtifactStore

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
            if (!_artifactCoords.ContainsKey(a.Id))
            {
                _artifactCoords[a.Id] = AllocateCoord(PhextSeries.Artifacts);
            }

            a.Updated = DateTime.UtcNow;
            _artifacts[a.Id] = a;

            var coord = _artifactCoords[a.Id];
            _phext = PhextEngine.Replace(_phext, coord, SerializeToScroll(a));
            SavePhextToDisk();

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

            if (_artifactCoords.TryGetValue(id, out var coord))
            {
                _phext = PhextEngine.Remove(_phext, coord);
                _artifactCoords.Remove(id);
            }

            SavePhextToDisk();
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

    #endregion

    #region IMessageStore

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
            if (!_messageCoords.ContainsKey(m.Id))
            {
                _messageCoords[m.Id] = AllocateCoord(PhextSeries.Messages);
            }

            m.Updated = DateTime.UtcNow;
            _messages[m.Id] = m;

            var coord = _messageCoords[m.Id];
            _phext = PhextEngine.Replace(_phext, coord, SerializeToScroll(m));
            SavePhextToDisk();

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

    #endregion

    #region Phext-specific Operations

    /// <summary>
    /// Gets the raw phext document for inspection or export
    /// </summary>
    public string GetRawPhext()
    {
        _lock.EnterReadLock();
        try
        {
            return _phext;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets a textmap of the current phext state
    /// </summary>
    public string GetTextmap()
    {
        _lock.EnterReadLock();
        try
        {
            return PhextEngine.Textmap(_phext);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Fetches content at a specific phext coordinate
    /// </summary>
    public string FetchAt(Coordinate coord)
    {
        _lock.EnterReadLock();
        try
        {
            return PhextEngine.Fetch(_phext, coord);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Compacts the phext document by rebuilding from scratch
    /// </summary>
    public void Compact()
    {
        _lock.EnterWriteLock();
        try
        {
            RebuildPhext();
            _logger.Info("PhextStore compacted");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    #endregion
}
