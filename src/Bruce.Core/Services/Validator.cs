using Bruce.Core.Configuration;
using Bruce.Core.Enums;
using Bruce.Core.Interfaces;
using Bruce.Core.Primitives;

namespace Bruce.Core.Services;

/// <summary>
/// Input validation for all entities
/// </summary>
public class Validator : IValidator
{
    private readonly BruceConfig _config;

    public Validator(BruceConfig config)
    {
        _config = config;
    }

    public Result ValidateTask(string title, string description, TaskSource source, TaskType type)
    {
        var builder = new ValidationBuilder();

        builder.Validate(Guard.NotNullOrWhitespace(title, "title"));
        builder.Validate(Guard.Length(title, 1, _config.MaxTitleLength, "title"));
        
        if (!string.IsNullOrEmpty(description))
        {
            builder.Validate(Guard.Length(description, 0, _config.MaxDescriptionLength, "description"));
        }

        builder.Validate(Guard.DefinedEnum(source, "source"));
        builder.Validate(Guard.DefinedEnum(type, "type"));

        return builder.Build();
    }

    public Result ValidateWorker(string name, WorkerType type, WorkerRole role)
    {
        var builder = new ValidationBuilder();

        builder.Validate(Guard.NotNullOrWhitespace(name, "name"));
        builder.Validate(Guard.Length(name, 1, _config.MaxWorkerNameLength, "name"));
        builder.Validate(Guard.DefinedEnum(type, "type"));
        builder.Validate(Guard.DefinedEnum(role, "role"));

        return builder.Build();
    }

    public Result ValidateMessage(string body, string fromWorkerId, string? toWorkerId)
    {
        var builder = new ValidationBuilder();

        builder.Validate(Guard.NotNullOrWhitespace(body, "body"));
        builder.Validate(Guard.Length(body, 1, _config.MaxMessageLength, "body"));
        builder.Validate(Guard.ValidWorkerId(fromWorkerId));
        
        if (toWorkerId != null)
        {
            builder.Validate(Guard.ValidWorkerId(toWorkerId));
        }

        return builder.Build();
    }

    /// <summary>
    /// Validate task ID format
    /// </summary>
    public static Result ValidateTaskId(string? id)
    {
        return Guard.ValidTaskId(id).IsSuccess 
            ? Result.Ok() 
            : Result.Invalid($"Invalid task ID: {id}");
    }

    /// <summary>
    /// Validate worker ID format
    /// </summary>
    public static Result ValidateWorkerId(string? id)
    {
        return Guard.ValidWorkerId(id).IsSuccess 
            ? Result.Ok() 
            : Result.Invalid($"Invalid worker ID: {id}");
    }
}
