using Bruce.Core.Enums;
using Bruce.Core.Models;
using Bruce.Core.Primitives;

namespace Bruce.Core.Interfaces;

/// <summary>
/// Base store interface with common operations
/// </summary>
public interface IStore<T> where T : EntityBase
{
    T? Get(string id);
    
    /// <summary>
    /// Save entity. Checks version for optimistic concurrency.
    /// </summary>
    Result Save(T entity);
    
    Result Delete(string id);
    
    IEnumerable<T> GetAll();
}

public interface ITaskStore : IStore<BruceTask>
{
    IEnumerable<BruceTask> GetByState(TaskState state);
    IEnumerable<BruceTask> GetByAssignee(string workerId);
    IEnumerable<BruceTask> GetUnassigned();
    IEnumerable<BruceTask> Query(Func<BruceTask, bool> predicate);
}

public interface IWorkerStore : IStore<Worker>
{
    IEnumerable<Worker> GetByType(WorkerType type);
    IEnumerable<Worker> GetByRole(WorkerRole role);
    IEnumerable<Worker> GetActive();
}

public interface IAssignmentStore
{
    Assignment? Get(string taskId);
    Result Save(Assignment assignment);
    Result Delete(string taskId);
    IEnumerable<Assignment> GetByWorker(string workerId);
    IEnumerable<Assignment> GetActive();  // Not completed
    IEnumerable<Assignment> GetCompleted(DateTime since);
}

public interface IArtifactStore : IStore<Artifact>
{
    IEnumerable<Artifact> GetByTask(string taskId);
}

public interface IMessageStore
{
    Message? Get(string id);
    Result Save(Message message);
    IEnumerable<Message> GetByTask(string taskId);
    IEnumerable<Message> GetForWorker(string workerId, DateTime since);
    IEnumerable<Message> GetBroadcasts(DateTime since);
    IEnumerable<Message> GetRecent(int count);
}
