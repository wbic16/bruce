namespace Bruce.Core.Configuration;

/// <summary>
/// Simple logging abstraction. Can be replaced with ILogger&lt;T&gt; later.
/// </summary>
public interface IBruceLogger
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
    None = 4
}

/// <summary>
/// Console logger implementation
/// </summary>
public class ConsoleLogger : IBruceLogger
{
    private readonly string _category;
    private readonly LogLevel _minLevel;

    public ConsoleLogger(string category, LogLevel minLevel = LogLevel.Info)
    {
        _category = category;
        _minLevel = minLevel;
    }

    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warn(string message) => Log(LogLevel.Warn, message);
    public void Error(string message, Exception? ex = null)
    {
        Log(LogLevel.Error, ex != null ? $"{message}: {ex.Message}" : message);
    }

    private void Log(LogLevel level, string message)
    {
        if (level < _minLevel) return;

        var color = level switch
        {
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Info => ConsoleColor.White,
            LogLevel.Warn => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            _ => ConsoleColor.White
        };

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var levelStr = level.ToString().ToUpper()[..4];

        var prevColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($"[{timestamp}] [{levelStr}] [{_category}] {message}");
        Console.ForegroundColor = prevColor;
    }
}

/// <summary>
/// Null logger for testing or when logging is disabled
/// </summary>
public class NullLogger : IBruceLogger
{
    public static readonly NullLogger Instance = new();
    public void Debug(string message) { }
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message, Exception? ex = null) { }
}

/// <summary>
/// Logger factory
/// </summary>
public class BruceLoggerFactory
{
    private readonly LogLevel _minLevel;

    public BruceLoggerFactory(LogLevel minLevel = LogLevel.Info)
    {
        _minLevel = minLevel;
    }

    public IBruceLogger Create(string category) => new ConsoleLogger(category, _minLevel);
    public IBruceLogger Create<T>() => Create(typeof(T).Name);
}
