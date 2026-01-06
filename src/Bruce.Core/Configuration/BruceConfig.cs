namespace Bruce.Core.Configuration;

/// <summary>
/// Configuration for Bruce engine.
/// Immutable after construction.
/// </summary>
public sealed class BruceConfig
{
    /// <summary>
    /// Path where data files are stored
    /// </summary>
    public string DataPath { get; init; } = "bruce_data";

    /// <summary>
    /// Minimum log level
    /// </summary>
    public LogLevel LogLevel { get; init; } = LogLevel.Info;

    /// <summary>
    /// Maximum title length for tasks
    /// </summary>
    public int MaxTitleLength { get; init; } = 200;

    /// <summary>
    /// Maximum description length for tasks
    /// </summary>
    public int MaxDescriptionLength { get; init; } = 10000;

    /// <summary>
    /// Maximum message body length
    /// </summary>
    public int MaxMessageLength { get; init; } = 5000;

    /// <summary>
    /// Maximum worker name length
    /// </summary>
    public int MaxWorkerNameLength { get; init; } = 100;

    /// <summary>
    /// How long to keep messages in context queries (hours)
    /// </summary>
    public int MessageRetentionHours { get; init; } = 24;

    /// <summary>
    /// Maximum tasks to return in context queries
    /// </summary>
    public int MaxContextTasks { get; init; } = 50;

    /// <summary>
    /// Enable atomic file writes (slightly slower but safer)
    /// </summary>
    public bool AtomicWrites { get; init; } = true;

    /// <summary>
    /// Lock timeout for concurrent operations (ms)
    /// </summary>
    public int LockTimeoutMs { get; init; } = 5000;

    /// <summary>
    /// Default configuration
    /// </summary>
    public static BruceConfig Default => new();

    /// <summary>
    /// Development configuration with debug logging
    /// </summary>
    public static BruceConfig Development => new()
    {
        LogLevel = LogLevel.Debug,
        DataPath = "bruce_dev_data"
    };

    /// <summary>
    /// Configuration builder for fluent construction
    /// </summary>
    public static ConfigBuilder Builder() => new();

    public class ConfigBuilder
    {
        private string _dataPath = "bruce_data";
        private LogLevel _logLevel = LogLevel.Info;
        private bool _atomicWrites = true;
        private int _maxTitleLength = 200;
        private int _maxDescriptionLength = 10000;
        private int _maxMessageLength = 5000;
        private int _maxWorkerNameLength = 100;
        private int _messageRetentionHours = 24;
        private int _maxContextTasks = 50;
        private int _lockTimeoutMs = 5000;

        public ConfigBuilder WithDataPath(string path)
        {
            _dataPath = path;
            return this;
        }

        public ConfigBuilder WithLogLevel(LogLevel level)
        {
            _logLevel = level;
            return this;
        }

        public ConfigBuilder WithAtomicWrites(bool enabled)
        {
            _atomicWrites = enabled;
            return this;
        }

        public BruceConfig Build() => new()
        {
            DataPath = _dataPath,
            LogLevel = _logLevel,
            AtomicWrites = _atomicWrites,
            MaxTitleLength = _maxTitleLength,
            MaxDescriptionLength = _maxDescriptionLength,
            MaxMessageLength = _maxMessageLength,
            MaxWorkerNameLength = _maxWorkerNameLength,
            MessageRetentionHours = _messageRetentionHours,
            MaxContextTasks = _maxContextTasks,
            LockTimeoutMs = _lockTimeoutMs
        };
    }
}
