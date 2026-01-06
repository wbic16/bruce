using Bruce.Core.Configuration;
using Bruce.Core.Enums;
using Bruce.Core.Interfaces;
using Bruce.Core.Models;
using Bruce.Core.Primitives;
using Bruce.Core.Services;

Console.WriteLine("=== Bruce DevOps Coordinator Demo ===\n");

// Initialize with development config (debug logging)
var config = BruceConfig.Builder()
    .WithDataPath("bruce_demo_data")
    .WithLogLevel(LogLevel.Info)
    .Build();

using var engine = new BruceEngine(config);

// Subscribe to events for visibility
engine.Events.SubscribeLegacy<TaskCreated>(e => 
    Console.WriteLine($"  [EVENT] Task created: {e.Task.Title}"));
engine.Events.SubscribeLegacy<TaskAssigned>(e => 
    Console.WriteLine($"  [EVENT] Task assigned to {e.Worker.Name} ({(e.Pushed ? "pushed" : "claimed")})"));
engine.Events.SubscribeLegacy<TaskStateChanged>(e => 
    Console.WriteLine($"  [EVENT] Task {e.Task.Id[..8]}... state: {e.OldState} -> {e.NewState}"));
engine.Events.SubscribeLegacy<MessageSent>(e => 
    Console.WriteLine($"  [EVENT] Message from {e.Message.FromWorkerId[..8]}...: {e.Message.Body[..Math.Min(30, e.Message.Body.Length)]}..."));

// === Create Workers ===
Console.WriteLine("\n--- Creating Workers ---");

var willResult = engine.CreateWorker("Will", WorkerType.Human, WorkerRole.Manager);
var aliceResult = engine.CreateWorker("Alice", WorkerType.Human, WorkerRole.Worker);
var bobResult = engine.CreateWorker("Bob", WorkerType.Human, WorkerRole.Worker);

// Handle results properly
if (willResult.IsFailure)
{
    Console.WriteLine($"Failed to create Will: {willResult.Error}");
    return;
}
var will = willResult.Value;

if (aliceResult.IsFailure || bobResult.IsFailure)
{
    Console.WriteLine("Failed to create human workers");
    return;
}
var alice = aliceResult.Value;
var bob = bobResult.Value;

Console.WriteLine($"  Human: {will.Name} @ {will.Coord} (capacity: {will.MaxCapacity})");
Console.WriteLine($"  Human: {alice.Name} @ {alice.Coord} (capacity: {alice.MaxCapacity})");
Console.WriteLine($"  Human: {bob.Name} @ {bob.Coord} (capacity: {bob.MaxCapacity})");

// Create AI workers
var aiWorkers = new List<Worker>();
var substrates = new[] { "claude", "gpt", "gemini", "grok", "llama", "mistral" };

foreach (var substrate in substrates)
{
    var result = engine.CreateWorker($"{substrate}-1", WorkerType.AI, WorkerRole.Worker, substrate);
    if (result.IsSuccess)
    {
        aiWorkers.Add(result.Value);
        Console.WriteLine($"  AI: {result.Value.Name} @ {result.Value.Coord} (substrate: {result.Value.Substrate})");
    }
}

// === Create Tasks ===
Console.WriteLine("\n--- Creating Tasks ---");

var taskResults = new[]
{
    engine.CreateTask("Fix login timeout bug", "Users report intermittent timeouts", TaskSource.Manual, TaskType.Adhoc),
    engine.CreateTask("Implement OAuth2", "Add support for OAuth2 authentication", TaskSource.Manual, TaskType.Planned),
    engine.CreateTask("Review PR #1234", "Security-sensitive changes to auth module", TaskSource.Manual, TaskType.Adhoc),
    engine.CreateTask("Update dependencies", "Q1 dependency refresh", TaskSource.Manual, TaskType.Planned),
    engine.CreateTask("Write API docs", "Document new REST endpoints", TaskSource.Manual, TaskType.Planned)
};

var tasks = new List<BruceTask>();
foreach (var r in taskResults)
{
    if (r.IsSuccess)
    {
        tasks.Add(r.Value);
        Console.WriteLine($"  Task: {r.Value.Id} - {r.Value.Title} ({r.Value.Type})");
    }
    else
    {
        Console.WriteLine($"  Failed: {r.Error}");
    }
}

// === Task State Machine Demo ===
Console.WriteLine("\n--- Task State Transitions ---");

// Show valid transitions
var firstTask = tasks[0];
var validTransitions = engine.GetValidTransitions(firstTask.Id);
Console.WriteLine($"  Task '{firstTask.Title}' valid transitions from {firstTask.State}: [{string.Join(", ", validTransitions)}]");

// Review tasks to make them assignable
foreach (var task in tasks)
{
    var reviewResult = engine.ReviewTask(task.Id);
    if (reviewResult.IsFailure)
    {
        Console.WriteLine($"  Failed to review {task.Id}: {reviewResult.Error}");
    }
}

// === Push Assignment Demo ===
Console.WriteLine("\n--- Push Assignments (Coordinator assigns) ---");

// Will (manager) assigns first task to Alice
var assignResult = engine.AssignTask(tasks[0].Id, alice.Id);
assignResult.Match(
    () => Console.WriteLine($"  Assigned '{tasks[0].Title}' to {alice.Name}"),
    (err, code) => Console.WriteLine($"  Assignment failed ({code}): {err}")
);

// Assign second task to Bob
assignResult = engine.AssignTask(tasks[1].Id, bob.Id);
assignResult.Match(
    () => Console.WriteLine($"  Assigned '{tasks[1].Title}' to {bob.Name}"),
    (err, code) => Console.WriteLine($"  Assignment failed ({code}): {err}")
);

// === Pull Assignment Demo ===
Console.WriteLine("\n--- Pull Assignments (Workers claim) ---");

// AI worker claims a task
var claimResult = engine.ClaimTask(aiWorkers[0].Id, TaskType.Adhoc);
claimResult.Match(
    task => Console.WriteLine($"  {aiWorkers[0].Name} claimed: {task.Title}"),
    (err, code) => Console.WriteLine($"  Claim failed ({code}): {err}")
);

// Another AI claims
claimResult = engine.ClaimTask(aiWorkers[1].Id, TaskType.Planned);
claimResult.Match(
    task => Console.WriteLine($"  {aiWorkers[1].Name} claimed: {task.Title}"),
    (err, code) => Console.WriteLine($"  Claim failed ({code}): {err}")
);

// === Capacity Checking ===
Console.WriteLine("\n--- Worker Capacity ---");

void ShowWorkerLoad(Worker w)
{
    var load = engine.GetWorkerLoad(w.Id);
    Console.WriteLine($"  {w.Name}: {load.Adhoc} adhoc, {load.Planned} planned, {load.Total}/{w.MaxCapacity} total");
}

ShowWorkerLoad(alice);
ShowWorkerLoad(bob);
ShowWorkerLoad(aiWorkers[0]);

// Try to exceed capacity
Console.WriteLine("\n--- Testing Capacity Limits ---");

// Fill up alice's slots
for (int i = 0; i < 5; i++)
{
    var extraTask = engine.CreateTask($"Extra task {i}", "Testing capacity", TaskSource.Manual, TaskType.Adhoc);
    if (extraTask.IsSuccess)
    {
        engine.ReviewTask(extraTask.Value.Id);
        var result = engine.AssignTask(extraTask.Value.Id, alice.Id);
        if (result.IsFailure)
        {
            Console.WriteLine($"  Expected capacity limit: {result.Error}");
            break;
        }
    }
}

ShowWorkerLoad(alice);

// === Messaging ===
Console.WriteLine("\n--- Messaging ---");

// Broadcast from Will
var msgResult = engine.Broadcast(will.Id, "Team sync: Please update your task status by EOD");
msgResult.Match(
    m => Console.WriteLine($"  Broadcast sent: {m.Id}"),
    (err, _) => Console.WriteLine($"  Broadcast failed: {err}")
);

// Direct message
msgResult = engine.SendMessage(alice.Id, "Need help with the OAuth implementation?", bob.Id, tasks[1].Id);
msgResult.Match(
    m => Console.WriteLine($"  DM sent: {m.Id}"),
    (err, _) => Console.WriteLine($"  DM failed: {err}")
);

// === Input Validation Demo ===
Console.WriteLine("\n--- Input Validation ---");

// Try to create task with empty title
var badTask = engine.CreateTask("", "No title", TaskSource.Manual, TaskType.Adhoc);
Console.WriteLine($"  Empty title: {(badTask.IsFailure ? $"Rejected - {badTask.Error}" : "Accepted (unexpected)")}");

// Try very long title
var longTitle = new string('x', 300);
badTask = engine.CreateTask(longTitle, "Too long", TaskSource.Manual, TaskType.Adhoc);
Console.WriteLine($"  Long title: {(badTask.IsFailure ? $"Rejected - {badTask.Error}" : "Accepted (unexpected)")}");

// Try invalid worker ID
var badAssign = engine.AssignTask(tasks[0].Id, "not-a-valid-id");
Console.WriteLine($"  Invalid worker: {(badAssign.IsFailure ? $"Rejected - {badAssign.Error}" : "Accepted (unexpected)")}");

// === System Summary ===
Console.WriteLine("\n--- System Summary ---");

var summary = engine.GetSystemSummary();
Console.WriteLine($"  Total tasks: {summary.TotalTasks}");
Console.WriteLine($"  By state: Created={summary.TasksByState_Created}, Reviewed={summary.TasksByState_Reviewed}, Assigned={summary.TasksByState_Assigned}");
Console.WriteLine($"  Workers: {summary.ActiveWorkers} active ({summary.HumanWorkers} human, {summary.AiWorkers} AI)");
Console.WriteLine($"  Address space remaining:");
foreach (var (entityType, (used, remaining)) in summary.AddressSpace)
{
    var name = entityType switch { 1 => "Users", 2 => "Tasks", 3 => "Artifacts", 4 => "Messages", 5 => "Archive", _ => $"Type{entityType}" };
    Console.WriteLine($"    {name}: {used} used, {remaining} remaining");
}

// === Task Advancement ===
Console.WriteLine("\n--- Task Lifecycle ---");

// Advance first task through states
var taskId = tasks[0].Id;
Console.WriteLine($"  Advancing task {taskId[..8]}...");

// Should be assigned -> testing
var advanceResult = engine.AdvanceTask(taskId);
advanceResult.Match(
    () => Console.WriteLine("    -> Testing"),
    (err, _) => Console.WriteLine($"    Failed: {err}")
);

// testing -> done
advanceResult = engine.AdvanceTask(taskId);
advanceResult.Match(
    () => Console.WriteLine("    -> Done"),
    (err, _) => Console.WriteLine($"    Failed: {err}")
);

// Try to advance done task (should fail)
advanceResult = engine.AdvanceTask(taskId);
advanceResult.Match(
    () => Console.WriteLine("    -> ??? (unexpected)"),
    (err, _) => Console.WriteLine($"    Expected: {err}")
);

// === Final Summary ===
Console.WriteLine("\n--- Final State ---");
summary = engine.GetSystemSummary();
Console.WriteLine($"  Tasks by state: Created={summary.TasksByState_Created}, Reviewed={summary.TasksByState_Reviewed}, Assigned={summary.TasksByState_Assigned}, Testing={summary.TasksByState_Testing}, Done={summary.TasksByState_Done}");

Console.WriteLine("\n=== Demo Complete ===");
