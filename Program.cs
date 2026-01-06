using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bruce.Cli;

/// <summary>
/// Bruce CLI - Command interface for hybrid human-AI team coordination
/// Sprint 2, Day 739 - January 6, 2026
/// Zero external dependencies - uses native .NET 8 only
/// </summary>
public class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string DataPath => Environment.GetEnvironmentVariable("BRUCE_DATA") 
        ?? Path.Combine(Directory.GetCurrentDirectory(), "demo_data");

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        try
        {
            return args[0].ToLower() switch
            {
                "worker" => await HandleWorker(args.Skip(1).ToArray()),
                "task" => await HandleTask(args.Skip(1).ToArray()),
                "message" or "msg" => await HandleMessage(args.Skip(1).ToArray()),
                "status" => await ShowStatus(),
                "context" => await ShowContext(args.Skip(1).ToArray()),
                "help" or "--help" or "-h" => ShowHelp(),
                _ => ShowHelp($"Unknown command: {args[0]}")
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    #region Command Handlers

    private static async Task<int> HandleWorker(string[] args)
    {
        if (args.Length == 0) return ShowHelp("worker requires a subcommand");

        return args[0].ToLower() switch
        {
            "list" or "ls" => await WorkerList(),
            "status" or "show" => await WorkerStatus(args.Skip(1).ToArray()),
            "register" or "add" => await WorkerRegister(args.Skip(1).ToArray()),
            _ => ShowHelp($"Unknown worker command: {args[0]}")
        };
    }

    private static async Task<int> HandleTask(string[] args)
    {
        if (args.Length == 0) return ShowHelp("task requires a subcommand");

        return args[0].ToLower() switch
        {
            "list" or "ls" => await TaskList(args.Skip(1).ToArray()),
            "create" or "add" => await TaskCreate(args.Skip(1).ToArray()),
            "show" => await TaskShow(args.Skip(1).ToArray()),
            "claim" => await TaskClaim(args.Skip(1).ToArray()),
            "release" => await TaskRelease(args.Skip(1).ToArray()),
            "advance" or "adv" => await TaskAdvance(args.Skip(1).ToArray()),
            _ => ShowHelp($"Unknown task command: {args[0]}")
        };
    }

    private static async Task<int> HandleMessage(string[] args)
    {
        if (args.Length == 0) return ShowHelp("message requires a subcommand");

        return args[0].ToLower() switch
        {
            "send" => await MessageSend(args.Skip(1).ToArray()),
            "list" or "ls" => await MessageList(args.Skip(1).ToArray()),
            _ => ShowHelp($"Unknown message command: {args[0]}")
        };
    }

    #endregion

    #region Worker Commands

    private static async Task<int> WorkerList()
    {
        var workers = await LoadWorkers();
        Console.WriteLine($"{"ID",-14} {"Name",-16} {"Role",-10} {"Substrate",-10} {"Status",-10}");
        Console.WriteLine(new string('-', 65));
        foreach (var w in workers)
        {
            Console.WriteLine($"{w.Id,-14} {w.Name,-16} {w.Role,-10} {w.Substrate,-10} {w.Status,-10}");
        }
        Console.WriteLine($"\nTotal: {workers.Count} workers");
        return 0;
    }

    private static async Task<int> WorkerStatus(string[] args)
    {
        if (args.Length == 0) return ShowHelp("worker status requires worker ID");
        
        var id = args[0];
        var workers = await LoadWorkers();
        var worker = workers.FirstOrDefault(w => 
            w.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ||
            w.Name.Equals(id, StringComparison.OrdinalIgnoreCase));
        
        if (worker == null)
        {
            Console.WriteLine($"Worker not found: {id}");
            return 1;
        }

        var assignments = await LoadAssignments();
        var workerAssignments = assignments.Where(a => a.WorkerId == worker.Id).ToList();

        Console.WriteLine($"Worker: {worker.Name}");
        Console.WriteLine($"  ID:           {worker.Id}");
        Console.WriteLine($"  Role:         {worker.Role}");
        Console.WriteLine($"  Substrate:    {worker.Substrate}");
        Console.WriteLine($"  Status:       {worker.Status}");
        Console.WriteLine($"  Endpoint:     {worker.Endpoint ?? "(none)"}");
        Console.WriteLine($"  Capabilities: {string.Join(", ", worker.Capabilities)}");
        Console.WriteLine($"  Assignments:  {workerAssignments.Count}");
        
        foreach (var a in workerAssignments)
            Console.WriteLine($"    - Task {a.TaskId} (since {a.AssignedAt:yyyy-MM-dd HH:mm})");
        
        return 0;
    }

    private static async Task<int> WorkerRegister(string[] args)
    {
        if (args.Length == 0) return ShowHelp("worker register requires a name");
        
        var name = args[0];
        var opts = ParseOptions(args.Skip(1).ToArray());
        
        var workers = await LoadWorkers();
        var newWorker = new Worker
        {
            Id = GenerateId("wrk"),
            Name = name,
            Role = opts.GetValueOrDefault("role", "Worker"),
            Substrate = opts.GetValueOrDefault("substrate", "Human"),
            Status = "Available",
            Endpoint = opts.GetValueOrDefault("endpoint", null),
            Capabilities = opts.GetValueOrDefault("cap", "general").Split(',').ToList()
        };
        
        workers.Add(newWorker);
        await SaveWorkers(workers);
        Console.WriteLine($"Registered worker: {newWorker.Name} ({newWorker.Id})");
        return 0;
    }

    #endregion

    #region Task Commands

    private static async Task<int> TaskList(string[] args)
    {
        var opts = ParseOptions(args);
        var tasks = await LoadTasks();
        
        if (opts.TryGetValue("state", out var state))
            tasks = tasks.Where(t => t.State.Equals(state, StringComparison.OrdinalIgnoreCase)).ToList();
        if (opts.TryGetValue("type", out var type))
            tasks = tasks.Where(t => t.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();

        Console.WriteLine($"{"ID",-18} {"Title",-30} {"State",-12} {"Type",-8} {"Pri",-4}");
        Console.WriteLine(new string('-', 76));
        foreach (var t in tasks.OrderByDescending(t => t.Priority).ThenBy(t => t.CreatedAt))
        {
            var title = t.Title.Length > 28 ? t.Title[..25] + "..." : t.Title;
            Console.WriteLine($"{t.Id,-18} {title,-30} {t.State,-12} {t.Type,-8} {t.Priority,-4}");
        }
        Console.WriteLine($"\nTotal: {tasks.Count} tasks");
        return 0;
    }

    private static async Task<int> TaskCreate(string[] args)
    {
        if (args.Length == 0) return ShowHelp("task create requires a title");
        
        var title = args[0];
        var opts = ParseOptions(args.Skip(1).ToArray());
        
        var tasks = await LoadTasks();
        var newTask = new BruceTask
        {
            Id = GenerateId("tsk"),
            Title = title,
            Description = opts.GetValueOrDefault("description", opts.GetValueOrDefault("d", "")),
            Type = opts.GetValueOrDefault("type", "AdHoc"),
            State = "Pending",
            Priority = int.TryParse(opts.GetValueOrDefault("priority", "5"), out var p) ? p : 5,
            ParentId = opts.GetValueOrDefault("parent", null),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        tasks.Add(newTask);
        await SaveTasks(tasks);
        
        Console.WriteLine($"Created task: {newTask.Id}");
        Console.WriteLine($"  Title: {newTask.Title}");
        Console.WriteLine($"  Type:  {newTask.Type}");
        Console.WriteLine($"  State: {newTask.State}");
        return 0;
    }

    private static async Task<int> TaskShow(string[] args)
    {
        if (args.Length == 0) return ShowHelp("task show requires task ID");
        
        var id = args[0];
        var tasks = await LoadTasks();
        var task = tasks.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        
        if (task == null)
        {
            Console.WriteLine($"Task not found: {id}");
            return 1;
        }

        var assignments = await LoadAssignments();
        var workers = await LoadWorkers();
        var taskAssignments = assignments.Where(a => a.TaskId == task.Id).ToList();

        Console.WriteLine($"Task: {task.Title}");
        Console.WriteLine($"  ID:          {task.Id}");
        Console.WriteLine($"  Type:        {task.Type}");
        Console.WriteLine($"  State:       {task.State}");
        Console.WriteLine($"  Priority:    {task.Priority}");
        Console.WriteLine($"  Created:     {task.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"  Updated:     {task.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        
        if (!string.IsNullOrEmpty(task.ParentId))
            Console.WriteLine($"  Parent:      {task.ParentId}");
        
        if (!string.IsNullOrEmpty(task.Description))
        {
            Console.WriteLine($"  Description:");
            foreach (var line in task.Description.Split('\n'))
                Console.WriteLine($"    {line}");
        }

        if (taskAssignments.Any())
        {
            Console.WriteLine($"  Assignments:");
            foreach (var a in taskAssignments)
            {
                var worker = workers.FirstOrDefault(w => w.Id == a.WorkerId);
                Console.WriteLine($"    - {worker?.Name ?? a.WorkerId} (since {a.AssignedAt:yyyy-MM-dd HH:mm})");
            }
        }
        return 0;
    }

    private static async Task<int> TaskClaim(string[] args)
    {
        if (args.Length == 0) return ShowHelp("task claim requires task ID");
        
        var taskId = args[0];
        var opts = ParseOptions(args.Skip(1).ToArray());
        
        if (!opts.TryGetValue("worker", out var workerId) && !opts.TryGetValue("w", out workerId))
            return ShowHelp("task claim requires --worker or -w option");

        var tasks = await LoadTasks();
        var task = tasks.FirstOrDefault(t => t.Id.Equals(taskId, StringComparison.OrdinalIgnoreCase));
        
        if (task == null)
        {
            Console.WriteLine($"Task not found: {taskId}");
            return 1;
        }

        var workers = await LoadWorkers();
        var worker = workers.FirstOrDefault(w => 
            w.Id.Equals(workerId, StringComparison.OrdinalIgnoreCase) ||
            w.Name.Equals(workerId, StringComparison.OrdinalIgnoreCase));

        if (worker == null)
        {
            Console.WriteLine($"Worker not found: {workerId}");
            return 1;
        }

        if (task.State != "Pending")
        {
            Console.WriteLine($"Task {taskId} is not available for claiming (state: {task.State})");
            return 1;
        }

        var assignments = await LoadAssignments();
        var newAssignment = new Assignment
        {
            Id = GenerateId("asn"),
            TaskId = task.Id,
            WorkerId = worker.Id,
            AssignedAt = DateTime.UtcNow
        };
        assignments.Add(newAssignment);
        await SaveAssignments(assignments);

        task.State = "Assigned";
        task.UpdatedAt = DateTime.UtcNow;
        await SaveTasks(tasks);

        Console.WriteLine($"Task {task.Id} claimed by {worker.Name}");
        Console.WriteLine($"  Assignment ID: {newAssignment.Id}");
        return 0;
    }

    private static async Task<int> TaskRelease(string[] args)
    {
        if (args.Length == 0) return ShowHelp("task release requires task ID");
        
        var taskId = args[0];
        var opts = ParseOptions(args.Skip(1).ToArray());
        
        if (!opts.TryGetValue("worker", out var workerId) && !opts.TryGetValue("w", out workerId))
            return ShowHelp("task release requires --worker or -w option");

        var tasks = await LoadTasks();
        var task = tasks.FirstOrDefault(t => t.Id.Equals(taskId, StringComparison.OrdinalIgnoreCase));
        
        if (task == null)
        {
            Console.WriteLine($"Task not found: {taskId}");
            return 1;
        }

        var workers = await LoadWorkers();
        var worker = workers.FirstOrDefault(w => 
            w.Id.Equals(workerId, StringComparison.OrdinalIgnoreCase) ||
            w.Name.Equals(workerId, StringComparison.OrdinalIgnoreCase));

        if (worker == null)
        {
            Console.WriteLine($"Worker not found: {workerId}");
            return 1;
        }

        var assignments = await LoadAssignments();
        var assignment = assignments.FirstOrDefault(a => 
            a.TaskId == task.Id && a.WorkerId == worker.Id);

        if (assignment == null)
        {
            Console.WriteLine($"No assignment found for {worker.Name} on task {task.Id}");
            return 1;
        }

        assignments.Remove(assignment);
        await SaveAssignments(assignments);

        task.State = "Pending";
        task.UpdatedAt = DateTime.UtcNow;
        await SaveTasks(tasks);

        Console.WriteLine($"Task {task.Id} released by {worker.Name}");
        return 0;
    }

    private static async Task<int> TaskAdvance(string[] args)
    {
        if (args.Length == 0) return ShowHelp("task advance requires task ID");
        
        var id = args[0];
        var opts = ParseOptions(args.Skip(1).ToArray());
        opts.TryGetValue("to", out var targetState);
        
        var tasks = await LoadTasks();
        var task = tasks.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        
        if (task == null)
        {
            Console.WriteLine($"Task not found: {id}");
            return 1;
        }

        var stateOrder = new[] { "Pending", "Assigned", "InProgress", "Review", "Done" };
        var currentIndex = Array.IndexOf(stateOrder, task.State);

        string newState;
        if (!string.IsNullOrEmpty(targetState))
        {
            var targetIndex = Array.FindIndex(stateOrder, s => 
                s.Equals(targetState, StringComparison.OrdinalIgnoreCase));
            if (targetIndex < 0)
            {
                Console.WriteLine($"Invalid state: {targetState}");
                Console.WriteLine($"Valid states: {string.Join(", ", stateOrder)}");
                return 1;
            }
            newState = stateOrder[targetIndex];
        }
        else
        {
            if (currentIndex >= stateOrder.Length - 1)
            {
                Console.WriteLine($"Task {id} is already at final state: {task.State}");
                return 0;
            }
            newState = stateOrder[currentIndex + 1];
        }

        var oldState = task.State;
        task.State = newState;
        task.UpdatedAt = DateTime.UtcNow;
        
        if (newState == "Done")
            task.CompletedAt = DateTime.UtcNow;

        await SaveTasks(tasks);
        Console.WriteLine($"Task {id}: {oldState} -> {newState}");
        return 0;
    }

    #endregion

    #region Message Commands

    private static async Task<int> MessageSend(string[] args)
    {
        if (args.Length < 2) return ShowHelp("message send requires <worker-id> <content>");
        
        var toWorkerId = args[0];
        var content = args[1];
        var opts = ParseOptions(args.Skip(2).ToArray());
        var fromWorkerId = opts.GetValueOrDefault("from", "system");
        opts.TryGetValue("task", out var taskId);

        var workers = await LoadWorkers();
        var toWorker = workers.FirstOrDefault(w => 
            w.Id.Equals(toWorkerId, StringComparison.OrdinalIgnoreCase) ||
            w.Name.Equals(toWorkerId, StringComparison.OrdinalIgnoreCase));

        if (toWorker == null && !toWorkerId.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Recipient not found: {toWorkerId}");
            return 1;
        }

        var messages = await LoadMessages();
        var newMessage = new Message
        {
            Id = GenerateId("msg"),
            FromWorkerId = fromWorkerId,
            ToWorkerId = toWorker?.Id ?? "all",
            Content = content,
            TaskId = taskId,
            SentAt = DateTime.UtcNow,
            Read = false
        };
        messages.Add(newMessage);
        await SaveMessages(messages);

        var recipientName = toWorker?.Name ?? "all workers";
        Console.WriteLine($"Message sent to {recipientName}");
        Console.WriteLine($"  ID: {newMessage.Id}");
        return 0;
    }

    private static async Task<int> MessageList(string[] args)
    {
        var opts = ParseOptions(args);
        opts.TryGetValue("worker", out var workerId);
        if (workerId == null) opts.TryGetValue("w", out workerId);
        var unreadOnly = opts.ContainsKey("unread");
        var limit = int.TryParse(opts.GetValueOrDefault("limit", "20"), out var l) ? l : 20;

        var messages = await LoadMessages();
        var workers = await LoadWorkers();

        if (!string.IsNullOrEmpty(workerId))
        {
            var worker = workers.FirstOrDefault(w => 
                w.Id.Equals(workerId, StringComparison.OrdinalIgnoreCase) ||
                w.Name.Equals(workerId, StringComparison.OrdinalIgnoreCase));
            
            if (worker != null)
            {
                messages = messages.Where(m => 
                    m.ToWorkerId == worker.Id || 
                    m.FromWorkerId == worker.Id ||
                    m.ToWorkerId == "all").ToList();
            }
        }

        if (unreadOnly)
            messages = messages.Where(m => !m.Read).ToList();

        messages = messages.OrderByDescending(m => m.SentAt).Take(limit).ToList();

        foreach (var m in messages)
        {
            var fromName = workers.FirstOrDefault(w => w.Id == m.FromWorkerId)?.Name ?? m.FromWorkerId;
            var toName = m.ToWorkerId == "all" ? "all" : 
                workers.FirstOrDefault(w => w.Id == m.ToWorkerId)?.Name ?? m.ToWorkerId;
            var readMarker = m.Read ? " " : "*";
            
            Console.WriteLine($"{readMarker}[{m.SentAt:MM-dd HH:mm}] {fromName} -> {toName}");
            Console.WriteLine($"  {m.Content}");
            if (!string.IsNullOrEmpty(m.TaskId))
                Console.WriteLine($"  (task: {m.TaskId})");
            Console.WriteLine();
        }

        Console.WriteLine($"Showing {messages.Count} messages");
        return 0;
    }

    #endregion

    #region System Commands

    private static async Task<int> ShowStatus()
    {
        var tasks = await LoadTasks();
        var workers = await LoadWorkers();
        var messages = await LoadMessages();
        var assignments = await LoadAssignments();

        Console.WriteLine("=== Bruce System Status ===");
        Console.WriteLine($"  Data Path: {DataPath}");
        Console.WriteLine();
        
        Console.WriteLine("Workers:");
        var bySubstrate = workers.GroupBy(w => w.Substrate);
        foreach (var group in bySubstrate)
        {
            var available = group.Count(w => w.Status == "Available");
            Console.WriteLine($"  {group.Key}: {group.Count()} ({available} available)");
        }
        Console.WriteLine();

        Console.WriteLine("Tasks:");
        foreach (var state in new[] { "Pending", "Assigned", "InProgress", "Review", "Done" })
        {
            var count = tasks.Count(t => t.State == state);
            if (count > 0)
                Console.WriteLine($"  {state}: {count}");
        }
        Console.WriteLine();

        Console.WriteLine($"Active Assignments: {assignments.Count}");
        Console.WriteLine($"Messages: {messages.Count} ({messages.Count(m => !m.Read)} unread)");
        Console.WriteLine();

        var recentTasks = tasks.OrderByDescending(t => t.UpdatedAt).Take(3);
        if (recentTasks.Any())
        {
            Console.WriteLine("Recent Activity:");
            foreach (var t in recentTasks)
                Console.WriteLine($"  [{t.State}] {t.Title}");
        }
        return 0;
    }

    private static async Task<int> ShowContext(string[] args)
    {
        if (args.Length == 0) return ShowHelp("context requires worker ID");

        var workerId = args[0];
        var workers = await LoadWorkers();
        var worker = workers.FirstOrDefault(w => 
            w.Id.Equals(workerId, StringComparison.OrdinalIgnoreCase) ||
            w.Name.Equals(workerId, StringComparison.OrdinalIgnoreCase));

        if (worker == null)
        {
            Console.WriteLine($"Worker not found: {workerId}");
            return 1;
        }

        var tasks = await LoadTasks();
        var assignments = await LoadAssignments();
        var messages = await LoadMessages();

        var workerAssignments = assignments.Where(a => a.WorkerId == worker.Id).ToList();
        var assignedTaskIds = workerAssignments.Select(a => a.TaskId).ToHashSet();
        var assignedTasks = tasks.Where(t => assignedTaskIds.Contains(t.Id)).ToList();
        var workerMessages = messages
            .Where(m => m.ToWorkerId == worker.Id || m.ToWorkerId == "all")
            .OrderByDescending(m => m.SentAt)
            .Take(10)
            .ToList();
        var pendingTasks = tasks.Where(t => t.State == "Pending").ToList();

        Console.WriteLine($"=== Context for {worker.Name} ({worker.Substrate}) ===");
        Console.WriteLine();

        if (assignedTasks.Any())
        {
            Console.WriteLine("Your Current Tasks:");
            foreach (var t in assignedTasks)
            {
                Console.WriteLine($"  [{t.State}] {t.Id}: {t.Title}");
                if (!string.IsNullOrEmpty(t.Description))
                {
                    var desc = t.Description.Length > 60 ? t.Description[..57] + "..." : t.Description;
                    Console.WriteLine($"           {desc}");
                }
            }
            Console.WriteLine();
        }

        if (pendingTasks.Any())
        {
            Console.WriteLine($"Available Tasks (claim with: bruce task claim <id> -w {worker.Id}):");
            foreach (var t in pendingTasks.OrderByDescending(t => t.Priority).Take(5))
                Console.WriteLine($"  [P{t.Priority}] {t.Id}: {t.Title}");
            Console.WriteLine();
        }

        if (workerMessages.Any(m => !m.Read))
        {
            Console.WriteLine("Unread Messages:");
            foreach (var m in workerMessages.Where(m => !m.Read))
            {
                var fromName = workers.FirstOrDefault(w => w.Id == m.FromWorkerId)?.Name ?? m.FromWorkerId;
                Console.WriteLine($"  [{m.SentAt:MM-dd HH:mm}] From {fromName}: {m.Content}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("Quick Actions:");
        Console.WriteLine($"  Claim task:    bruce task claim <task-id> -w {worker.Id}");
        Console.WriteLine($"  Advance task:  bruce task advance <task-id>");
        Console.WriteLine($"  Send message:  bruce message send <worker-id> \"message\" --from {worker.Id}");
        return 0;
    }

    private static int ShowHelp(string? error = null)
    {
        if (error != null)
            Console.WriteLine($"Error: {error}\n");

        Console.WriteLine(@"Bruce CLI - Hybrid Human-AI Team Coordination
Sprint 2, Day 739 - January 6, 2026

Usage: bruce <command> [options]

Commands:
  worker list                           List all workers
  worker status <id>                    Show worker details
  worker register <n> [--substrate]     Register new worker

  task list [--state <s>] [--type <t>]  List tasks
  task create <title> [--type] [-d]     Create new task
  task show <id>                        Show task details
  task claim <id> -w <worker>           Claim a task
  task release <id> -w <worker>         Release a task  
  task advance <id> [--to <state>]      Advance task state

  message send <worker> <content>       Send message
  message list [-w <worker>] [--unread] List messages

  status                                System summary
  context <worker-id>                   Worker's current context

Task States: Pending -> Assigned -> InProgress -> Review -> Done

Environment:
  BRUCE_DATA    Data directory (default: ./demo_data/)

Examples:
  bruce status
  bruce task list --state Pending
  bruce task claim tsk_abc -w wrk_claudex2
  bruce task advance tsk_abc --to InProgress
  bruce message send wrk_will ""Done!"" --from wrk_claudex2");
        
        return error != null ? 1 : 0;
    }

    #endregion

    #region Utilities

    private static Dictionary<string, string?> ParseOptions(string[] args)
    {
        var opts = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--"))
            {
                var key = arg[2..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    opts[key] = args[++i];
                else
                    opts[key] = "true";
            }
            else if (arg.StartsWith("-") && arg.Length == 2)
            {
                var key = arg[1..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    opts[key] = args[++i];
                else
                    opts[key] = "true";
            }
        }
        return opts;
    }

    private static string GenerateId(string prefix)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var random = Random.Shared.Next(0x1000, 0xFFFF).ToString("x4");
        return $"{prefix}_{timestamp:x}_{random}";
    }

    #endregion

    #region Data Access

    private static async Task<List<Worker>> LoadWorkers()
    {
        var path = Path.Combine(DataPath, "workers.json");
        if (!File.Exists(path)) return new List<Worker>();
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<Worker>>(json, JsonOptions) ?? new List<Worker>();
    }

    private static async Task SaveWorkers(List<Worker> workers)
    {
        Directory.CreateDirectory(DataPath);
        var path = Path.Combine(DataPath, "workers.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(workers, JsonOptions));
    }

    private static async Task<List<BruceTask>> LoadTasks()
    {
        var path = Path.Combine(DataPath, "tasks.json");
        if (!File.Exists(path)) return new List<BruceTask>();
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<BruceTask>>(json, JsonOptions) ?? new List<BruceTask>();
    }

    private static async Task SaveTasks(List<BruceTask> tasks)
    {
        Directory.CreateDirectory(DataPath);
        var path = Path.Combine(DataPath, "tasks.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(tasks, JsonOptions));
    }

    private static async Task<List<Assignment>> LoadAssignments()
    {
        var path = Path.Combine(DataPath, "assignments.json");
        if (!File.Exists(path)) return new List<Assignment>();
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<Assignment>>(json, JsonOptions) ?? new List<Assignment>();
    }

    private static async Task SaveAssignments(List<Assignment> assignments)
    {
        Directory.CreateDirectory(DataPath);
        var path = Path.Combine(DataPath, "assignments.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(assignments, JsonOptions));
    }

    private static async Task<List<Message>> LoadMessages()
    {
        var path = Path.Combine(DataPath, "messages.json");
        if (!File.Exists(path)) return new List<Message>();
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<Message>>(json, JsonOptions) ?? new List<Message>();
    }

    private static async Task SaveMessages(List<Message> messages)
    {
        Directory.CreateDirectory(DataPath);
        var path = Path.Combine(DataPath, "messages.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(messages, JsonOptions));
    }

    #endregion
}

#region Models

public class Worker
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "Worker";
    public string Substrate { get; set; } = "Human";
    public string Status { get; set; } = "Available";
    public string? Endpoint { get; set; }
    public List<string> Capabilities { get; set; } = new();
}

public class BruceTask
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "AdHoc";
    public string State { get; set; } = "Pending";
    public int Priority { get; set; } = 5;
    public string? ParentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class Assignment
{
    public string Id { get; set; } = "";
    public string TaskId { get; set; } = "";
    public string WorkerId { get; set; } = "";
    public DateTime AssignedAt { get; set; }
}

public class Message
{
    public string Id { get; set; } = "";
    public string FromWorkerId { get; set; } = "";
    public string ToWorkerId { get; set; } = "";
    public string Content { get; set; } = "";
    public string? TaskId { get; set; }
    public DateTime SentAt { get; set; }
    public bool Read { get; set; }
}

#endregion
