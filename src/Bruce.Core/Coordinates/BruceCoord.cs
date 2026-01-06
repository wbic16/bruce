using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bruce.Core.Coordinates;

/// <summary>
/// Phext-compatible coordinate in format L.S.R/L.S.R/L.S.R
/// For Bruce: fixed at 1.1.1/1.1.X/Y.Z.W where X=entity type
/// 
/// Expanded address space: 9 * 9 * 9 = 729 slots per entity type
/// </summary>
[JsonConverter(typeof(BruceCoordConverter))]
public readonly struct BruceCoord : IEquatable<BruceCoord>, IComparable<BruceCoord>
{
    // Entity type markers (scroll position in L2)
    public const int Users = 1;
    public const int Tasks = 2;
    public const int Artifacts = 3;
    public const int Messages = 4;
    public const int Archive = 5;  // For completed tasks

    // L3 coordinates: Y.Z.W
    public int EntityType { get; }  // 1-9 (scroll in L2)
    public int Y { get; }           // 1-9
    public int Z { get; }           // 1-9  
    public int W { get; }           // 1-9

    public BruceCoord(int entityType, int y, int z, int w)
    {
        ValidateRange(entityType, nameof(entityType));
        ValidateRange(y, nameof(y));
        ValidateRange(z, nameof(z));
        ValidateRange(w, nameof(w));

        EntityType = entityType;
        Y = y;
        Z = z;
        W = w;
    }

    private static void ValidateRange(int value, string name)
    {
        if (value < 1 || value > 9)
            throw new ArgumentOutOfRangeException(name, value, "Must be between 1 and 9");
    }

    /// <summary>Full phext address</summary>
    public string ToPhext() => $"1.1.1/1.1.{EntityType}/{Y}.{Z}.{W}";

    /// <summary>Short form for storage and display: E.Y.Z.W</summary>
    public string ToShort() => $"{EntityType}.{Y}.{Z}.{W}";

    /// <summary>Numeric value for sorting (entity type * 1000 + linear position)</summary>
    public int ToSortKey() => EntityType * 1000 + (Y - 1) * 81 + (Z - 1) * 9 + (W - 1);

    /// <summary>Linear position within entity type (0-728)</summary>
    public int LinearPosition => (Y - 1) * 81 + (Z - 1) * 9 + (W - 1);

    public static BruceCoord FromLinear(int entityType, int position)
    {
        if (position < 0 || position >= 729)
            throw new ArgumentOutOfRangeException(nameof(position), "Must be 0-728");

        var y = position / 81 + 1;
        var remainder = position % 81;
        var z = remainder / 9 + 1;
        var w = remainder % 9 + 1;

        return new BruceCoord(entityType, y, z, w);
    }

    public static BruceCoord Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException("Coordinate cannot be null or empty");

        var parts = value.Split('.');
        
        // Support both old format (E.Y.Z) and new format (E.Y.Z.W)
        if (parts.Length == 3)
        {
            // Old format: E.Y.Z -> convert to E.Y.Z.1
            return new BruceCoord(
                int.Parse(parts[0]),
                int.Parse(parts[1]),
                int.Parse(parts[2]),
                1
            );
        }
        
        if (parts.Length != 4)
            throw new FormatException($"Invalid coordinate format: {value} (expected E.Y.Z.W)");

        try
        {
            return new BruceCoord(
                int.Parse(parts[0]),
                int.Parse(parts[1]),
                int.Parse(parts[2]),
                int.Parse(parts[3])
            );
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentOutOfRangeException)
        {
            throw new FormatException($"Invalid coordinate: {value}", ex);
        }
    }

    public static bool TryParse(string? value, out BruceCoord coord)
    {
        coord = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            coord = Parse(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Equals(BruceCoord other) =>
        EntityType == other.EntityType && Y == other.Y && Z == other.Z && W == other.W;

    public override bool Equals(object? obj) => obj is BruceCoord c && Equals(c);

    public override int GetHashCode() => HashCode.Combine(EntityType, Y, Z, W);

    public int CompareTo(BruceCoord other) => ToSortKey().CompareTo(other.ToSortKey());

    public override string ToString() => ToShort();

    public static bool operator ==(BruceCoord a, BruceCoord b) => a.Equals(b);
    public static bool operator !=(BruceCoord a, BruceCoord b) => !a.Equals(b);
    public static bool operator <(BruceCoord a, BruceCoord b) => a.CompareTo(b) < 0;
    public static bool operator >(BruceCoord a, BruceCoord b) => a.CompareTo(b) > 0;
    public static bool operator <=(BruceCoord a, BruceCoord b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BruceCoord a, BruceCoord b) => a.CompareTo(b) >= 0;

    /// <summary>
    /// Returns next coordinate in sequence, or null if at max
    /// </summary>
    public BruceCoord? Next()
    {
        var nextLinear = LinearPosition + 1;
        if (nextLinear >= 729)
            return null;
        return FromLinear(EntityType, nextLinear);
    }
}

/// <summary>
/// JSON converter for BruceCoord
/// </summary>
public class BruceCoordConverter : JsonConverter<BruceCoord>
{
    public override BruceCoord Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
            throw new JsonException("BruceCoord cannot be null or empty");
        return BruceCoord.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, BruceCoord value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToShort());
    }
}

/// <summary>
/// Thread-safe coordinate allocator
/// </summary>
public class CoordAllocator
{
    private readonly object _lock = new();
    private readonly Dictionary<int, int> _nextPosition = new()
    {
        [BruceCoord.Users] = 0,
        [BruceCoord.Tasks] = 0,
        [BruceCoord.Artifacts] = 0,
        [BruceCoord.Messages] = 0,
        [BruceCoord.Archive] = 0
    };

    /// <summary>
    /// Allocate next available coordinate for entity type.
    /// Thread-safe.
    /// </summary>
    public BruceCoord Allocate(int entityType)
    {
        lock (_lock)
        {
            if (!_nextPosition.TryGetValue(entityType, out var position))
                throw new ArgumentException($"Unknown entity type: {entityType}");

            if (position >= 729)
                throw new InvalidOperationException($"Address space exhausted for entity type {entityType}");

            var coord = BruceCoord.FromLinear(entityType, position);
            _nextPosition[entityType] = position + 1;
            return coord;
        }
    }

    /// <summary>
    /// Set high water mark (for loading existing data).
    /// Thread-safe.
    /// </summary>
    public void SetHighWater(int entityType, int position)
    {
        lock (_lock)
        {
            if (!_nextPosition.ContainsKey(entityType))
                throw new ArgumentException($"Unknown entity type: {entityType}");

            if (position >= 0 && position < 729)
                _nextPosition[entityType] = Math.Max(_nextPosition[entityType], position + 1);
        }
    }

    /// <summary>
    /// Get remaining capacity for entity type
    /// </summary>
    public int GetRemaining(int entityType)
    {
        lock (_lock)
        {
            return _nextPosition.TryGetValue(entityType, out var pos) ? 729 - pos : 0;
        }
    }

    /// <summary>
    /// Get current allocation stats
    /// </summary>
    public Dictionary<int, (int used, int remaining)> GetStats()
    {
        lock (_lock)
        {
            return _nextPosition.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value, 729 - kvp.Value)
            );
        }
    }
}
