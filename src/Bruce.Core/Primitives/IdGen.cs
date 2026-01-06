namespace Bruce.Core.Primitives;

/// <summary>
/// Generates consistent, prefixed IDs for all entity types.
/// Format: {prefix}-{timestamp-base36}-{random}
/// Example: T-2k5x9-a7b3
/// </summary>
public static class IdGen
{
    private static readonly object Lock = new();
    private static long _lastTimestamp;
    private static int _sequence;

    private const string TaskPrefix = "T";
    private const string WorkerPrefix = "W";
    private const string AssignmentPrefix = "A";
    private const string MessagePrefix = "M";
    private const string ArtifactPrefix = "F";  // F for File

    public static string Task() => Generate(TaskPrefix);
    public static string Worker() => Generate(WorkerPrefix);
    public static string Assignment() => Generate(AssignmentPrefix);
    public static string Message() => Generate(MessagePrefix);
    public static string Artifact() => Generate(ArtifactPrefix);

    /// <summary>
    /// Extracts entity type from ID prefix
    /// </summary>
    public static string? GetEntityType(string id)
    {
        if (string.IsNullOrEmpty(id) || !id.Contains('-'))
            return null;

        return id[..id.IndexOf('-')] switch
        {
            TaskPrefix => "Task",
            WorkerPrefix => "Worker",
            AssignmentPrefix => "Assignment",
            MessagePrefix => "Message",
            ArtifactPrefix => "Artifact",
            _ => null
        };
    }

    /// <summary>
    /// Validates that an ID matches expected entity type
    /// </summary>
    public static bool IsValidTaskId(string? id) => id?.StartsWith(TaskPrefix + "-") == true;
    public static bool IsValidWorkerId(string? id) => id?.StartsWith(WorkerPrefix + "-") == true;
    public static bool IsValidMessageId(string? id) => id?.StartsWith(MessagePrefix + "-") == true;
    public static bool IsValidArtifactId(string? id) => id?.StartsWith(ArtifactPrefix + "-") == true;

    private static string Generate(string prefix)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int seq;

        lock (Lock)
        {
            if (timestamp == _lastTimestamp)
            {
                _sequence++;
            }
            else
            {
                _lastTimestamp = timestamp;
                _sequence = 0;
            }
            seq = _sequence;
        }

        // Combine timestamp and sequence into a sortable component
        var timeComponent = ToBase36(timestamp).PadLeft(8, '0')[^6..]; // Last 6 chars
        var seqComponent = ToBase36(seq).PadLeft(2, '0')[^2..];
        var randomComponent = ToBase36(Random.Shared.NextInt64(0, 36 * 36 * 36 * 36)).PadLeft(4, '0')[^4..];

        return $"{prefix}-{timeComponent}{seqComponent}-{randomComponent}";
    }

    private static string ToBase36(long value)
    {
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (value == 0) return "0";

        var result = new char[13];
        var i = 12;
        var absValue = Math.Abs(value);

        while (absValue > 0)
        {
            result[i--] = chars[(int)(absValue % 36)];
            absValue /= 36;
        }

        return new string(result, i + 1, 12 - i);
    }
}
