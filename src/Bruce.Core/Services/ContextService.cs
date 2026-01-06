using Bruce.Core.Configuration;
using Bruce.Core.Coordinates;
using Bruce.Core.Enums;
using Bruce.Core.Interfaces;
using Bruce.Core.Models;

namespace Bruce.Core.Services;

/// <summary>
/// Aggregates context information for workers and system-wide views.
/// </summary>
public class ContextService : IContextService
{
    private readonly ITaskStore _tasks;
    private readonly IWorkerStore _workers;
    private readonly IMessageStore _messages;
    private readonly IAssignmentStore _assignments;
    private readonly IDistributor _distributor;
    private readonly CoordAllocator _allocator;
    private readonly BruceConfig _config;

    public ContextService(
        ITaskStore tasks,
        IWorkerStore workers,
        IMessageStore messages,
        IAssignmentStore assignments,
        IDistributor distributor,
        CoordAllocator allocator,
        BruceConfig config)
    {
        _tasks = tasks;
        _workers = workers;
        _messages = messages;
        _assignments = assignments;
        _distributor = distributor;
        _allocator = allocator;
        _config = config;
    }

    public WorkerContext GetContext(string workerId)
    {
        var since = DateTime.UtcNow.AddHours(-_config.MessageRetentionHours);

        var assigned = _tasks.GetByAssignee(workerId)
            .Where(t => t.State != TaskState.Done)
            .OrderBy(t => t.Created)
            .ToList();

        var available = _tasks.GetUnassigned()
            .OrderBy(t => t.Created)
            .Take(_config.MaxContextTasks)
            .ToList();

        var messages = _messages.GetForWorker(workerId, since).ToList();
        var load = _distributor.GetLoad(workerId);

        return new WorkerContext
        {
            WorkerId = workerId,
            AssignedTasks = assigned,
            AvailableTasks = available,
            RecentMessages = messages,
            Load = load,
            AsOf = DateTime.UtcNow
        };
    }

    public WorkerContext GetSharedContext()
    {
        var since = DateTime.UtcNow.AddHours(-_config.MessageRetentionHours);

        // All non-done tasks
        var allActive = _tasks.Query(t => t.State != TaskState.Done)
            .OrderByDescending(t => t.Updated)
            .Take(_config.MaxContextTasks)
            .ToList();

        var broadcasts = _messages.GetBroadcasts(since).ToList();

        return new WorkerContext
        {
            WorkerId = "_shared",
            AssignedTasks = allActive.Where(t => t.AssignedTo != null).ToList(),
            AvailableTasks = allActive.Where(t => t.AssignedTo == null).ToList(),
            RecentMessages = broadcasts,
            Load = new WorkerLoad(),
            AsOf = DateTime.UtcNow
        };
    }

    public SystemSummaryDto GetSummary()
    {
        var allTasks = _tasks.GetAll().ToList();
        var allWorkers = ((IStore<Worker>)_workers).GetAll().ToList();
        var recentMessages = _messages.GetRecent(100).ToList();
        var since = DateTime.UtcNow.AddHours(-_config.MessageRetentionHours);

        var tasksByState = allTasks
            .GroupBy(t => t.State)
            .ToDictionary(g => g.Key, g => g.Count());

        var unreadMessages = recentMessages.Count(m => m.Timestamp > since);

        return new SystemSummaryDto(
            TotalTasks: allTasks.Count,
            TasksByState_Created: tasksByState.GetValueOrDefault(TaskState.Created),
            TasksByState_Reviewed: tasksByState.GetValueOrDefault(TaskState.Reviewed),
            TasksByState_Assigned: tasksByState.GetValueOrDefault(TaskState.Assigned),
            TasksByState_Testing: tasksByState.GetValueOrDefault(TaskState.Testing),
            TasksByState_Done: tasksByState.GetValueOrDefault(TaskState.Done),
            TotalWorkers: allWorkers.Count,
            ActiveWorkers: allWorkers.Count(w => w.IsActive),
            HumanWorkers: allWorkers.Count(w => w.Type == WorkerType.Human),
            AiWorkers: allWorkers.Count(w => w.Type == WorkerType.AI),
            UnreadMessages: unreadMessages,
            AddressSpace: _allocator.GetStats()
        );
    }
}
