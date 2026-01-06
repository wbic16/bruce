namespace Bruce.Core.Enums;

public enum TaskState
{
    Created,
    Reviewed,
    Assigned,
    Testing,
    Done
}

public enum TaskType
{
    Adhoc,
    Planned
}

public enum TaskSource
{
    Manual,
    Teams,
    Email,
    Helix
}

public enum WorkerType
{
    Human,
    AI
}

public enum WorkerRole
{
    Worker,   // 2 adhoc + 2 planned
    Manager,  // 24 any
    Director  // 200 any
}
