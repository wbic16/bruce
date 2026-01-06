using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Bruce.Core.Primitives;

/// <summary>
/// Fluent validation guard clauses.
/// Returns Result to allow for error aggregation without exceptions.
/// </summary>
public static class Guard
{
    /// <summary>
    /// Validates a string is not null or empty
    /// </summary>
    public static Result<string> NotNullOrEmpty(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrEmpty(value))
            return Result<string>.Invalid($"{paramName} cannot be null or empty");
        return value;
    }

    /// <summary>
    /// Validates a string is not null, empty, or whitespace
    /// </summary>
    public static Result<string> NotNullOrWhitespace(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<string>.Invalid($"{paramName} cannot be null, empty, or whitespace");
        return value;
    }

    /// <summary>
    /// Validates string length is within bounds
    /// </summary>
    public static Result<string> Length(
        string? value,
        int min,
        int max,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == null)
            return Result<string>.Invalid($"{paramName} cannot be null");
        if (value.Length < min || value.Length > max)
            return Result<string>.Invalid($"{paramName} must be between {min} and {max} characters (was {value.Length})");
        return value;
    }

    /// <summary>
    /// Validates a value is not null
    /// </summary>
    public static Result<T> NotNull<T>(
        [NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : class
    {
        if (value == null)
            return Result<T>.Invalid($"{paramName} cannot be null");
        return value;
    }

    /// <summary>
    /// Validates a nullable value type has a value
    /// </summary>
    public static Result<T> HasValue<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : struct
    {
        if (!value.HasValue)
            return Result<T>.Invalid($"{paramName} must have a value");
        return value.Value;
    }

    /// <summary>
    /// Validates a number is within range
    /// </summary>
    public static Result<int> InRange(
        int value,
        int min,
        int max,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < min || value > max)
            return Result<int>.Invalid($"{paramName} must be between {min} and {max} (was {value})");
        return value;
    }

    /// <summary>
    /// Validates a condition is true
    /// </summary>
    public static Result Condition(bool condition, string errorMessage)
    {
        return condition ? Result.Ok() : Result.Invalid(errorMessage);
    }

    /// <summary>
    /// Validates an enum value is defined
    /// </summary>
    public static Result<TEnum> DefinedEnum<TEnum>(
        TEnum value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            return Result<TEnum>.Invalid($"{paramName} has invalid value: {value}");
        return value;
    }

    /// <summary>
    /// Validates ID format matches expected entity type
    /// </summary>
    public static Result<string> ValidTaskId(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return Result<string>.Invalid("Task ID cannot be null or empty");
        if (!IdGen.IsValidTaskId(id))
            return Result<string>.Invalid($"Invalid task ID format: {id}");
        return id;
    }

    public static Result<string> ValidWorkerId(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return Result<string>.Invalid("Worker ID cannot be null or empty");
        if (!IdGen.IsValidWorkerId(id))
            return Result<string>.Invalid($"Invalid worker ID format: {id}");
        return id;
    }
}

/// <summary>
/// Aggregates multiple validation results
/// </summary>
public class ValidationBuilder
{
    private readonly List<string> _errors = new();

    public ValidationBuilder Validate(Result result)
    {
        if (result.IsFailure)
            _errors.Add(result.Error);
        return this;
    }

    public ValidationBuilder Validate<T>(Result<T> result)
    {
        if (result.IsFailure)
            _errors.Add(result.Error);
        return this;
    }

    public ValidationBuilder ValidateIf(bool condition, Func<Result> validator)
    {
        if (condition)
            return Validate(validator());
        return this;
    }

    public bool HasErrors => _errors.Count > 0;
    public IReadOnlyList<string> Errors => _errors;

    public Result Build()
    {
        if (_errors.Count == 0)
            return Result.Ok();
        return Result.Invalid(string.Join("; ", _errors));
    }

    public Result<T> Build<T>(Func<T> valueFactory)
    {
        if (_errors.Count == 0)
            return Result<T>.Ok(valueFactory());
        return Result<T>.Invalid(string.Join("; ", _errors));
    }
}
