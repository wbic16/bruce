namespace Bruce.Core.Primitives;

/// <summary>
/// Represents the outcome of an operation that can fail.
/// Forces callers to handle both success and failure cases.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly string? _error;
    private readonly ResultErrorCode _errorCode;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess 
        ? _value! 
        : throw new InvalidOperationException($"Cannot access Value on failed result: {_error}");

    public string Error => IsFailure 
        ? _error! 
        : throw new InvalidOperationException("Cannot access Error on successful result");

    public ResultErrorCode ErrorCode => _errorCode;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
        _errorCode = ResultErrorCode.None;
    }

    private Result(string error, ResultErrorCode code)
    {
        IsSuccess = false;
        _value = default;
        _error = error;
        _errorCode = code;
    }

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(string error, ResultErrorCode code = ResultErrorCode.Unknown) => new(error, code);

    public static Result<T> NotFound(string message) => new(message, ResultErrorCode.NotFound);
    public static Result<T> Invalid(string message) => new(message, ResultErrorCode.ValidationFailed);
    public static Result<T> Conflict(string message) => new(message, ResultErrorCode.Conflict);
    public static Result<T> CapacityExceeded(string message) => new(message, ResultErrorCode.CapacityExceeded);
    public static Result<T> InvalidState(string message) => new(message, ResultErrorCode.InvalidStateTransition);

    public Result<TNew> Map<TNew>(Func<T, TNew> mapper) =>
        IsSuccess ? Result<TNew>.Ok(mapper(_value!)) : Result<TNew>.Fail(_error!, _errorCode);

    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder) =>
        IsSuccess ? binder(_value!) : Result<TNew>.Fail(_error!, _errorCode);

    public T GetValueOrDefault(T defaultValue) => IsSuccess ? _value! : defaultValue;

    public void Match(Action<T> onSuccess, Action<string, ResultErrorCode> onFailure)
    {
        if (IsSuccess) onSuccess(_value!);
        else onFailure(_error!, _errorCode);
    }

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, ResultErrorCode, TResult> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_error!, _errorCode);

    public static implicit operator Result<T>(T value) => Ok(value);

    public override string ToString() => IsSuccess ? $"Ok({_value})" : $"Fail({_errorCode}: {_error})";
}

/// <summary>
/// Non-generic Result for void operations
/// </summary>
public readonly struct Result
{
    private readonly string? _error;
    private readonly ResultErrorCode _errorCode;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access Error on successful result");
    public ResultErrorCode ErrorCode => _errorCode;

    private Result(bool success, string? error, ResultErrorCode code)
    {
        IsSuccess = success;
        _error = error;
        _errorCode = code;
    }

    public static Result Ok() => new(true, null, ResultErrorCode.None);
    public static Result Fail(string error, ResultErrorCode code = ResultErrorCode.Unknown) => new(false, error, code);
    public static Result NotFound(string message) => new(false, message, ResultErrorCode.NotFound);
    public static Result Invalid(string message) => new(false, message, ResultErrorCode.ValidationFailed);
    public static Result Conflict(string message) => new(false, message, ResultErrorCode.Conflict);
    public static Result CapacityExceeded(string message) => new(false, message, ResultErrorCode.CapacityExceeded);
    public static Result InvalidState(string message) => new(false, message, ResultErrorCode.InvalidStateTransition);

    public Result<T> Map<T>(Func<T> mapper) =>
        IsSuccess ? Result<T>.Ok(mapper()) : Result<T>.Fail(_error!, _errorCode);

    public void Match(Action onSuccess, Action<string, ResultErrorCode> onFailure)
    {
        if (IsSuccess) onSuccess();
        else onFailure(_error!, _errorCode);
    }

    public override string ToString() => IsSuccess ? "Ok" : $"Fail({_errorCode}: {_error})";
}

public enum ResultErrorCode
{
    None = 0,
    Unknown = 1,
    NotFound = 100,
    ValidationFailed = 200,
    InvalidStateTransition = 201,
    Conflict = 300,
    CapacityExceeded = 301,
    ConcurrencyConflict = 302,
    PersistenceFailed = 400
}
