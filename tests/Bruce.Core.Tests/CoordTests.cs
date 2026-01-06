using Bruce.Core.Coordinates;
using System.Text.Json;
using Xunit;

namespace Bruce.Core.Tests;

public class BruceCoordTests
{
    [Fact]
    public void Constructor_ValidValues_CreatesCoord()
    {
        var coord = new BruceCoord(2, 3, 4, 5);
        
        Assert.Equal(2, coord.EntityType);
        Assert.Equal(3, coord.Y);
        Assert.Equal(4, coord.Z);
        Assert.Equal(5, coord.W);
    }

    [Theory]
    [InlineData(0, 1, 1, 1)]  // EntityType too low
    [InlineData(10, 1, 1, 1)] // EntityType too high
    [InlineData(1, 0, 1, 1)]  // Y too low
    [InlineData(1, 10, 1, 1)] // Y too high
    [InlineData(1, 1, 0, 1)]  // Z too low
    [InlineData(1, 1, 10, 1)] // Z too high
    [InlineData(1, 1, 1, 0)]  // W too low
    [InlineData(1, 1, 1, 10)] // W too high
    public void Constructor_InvalidValues_ThrowsArgumentOutOfRange(int e, int y, int z, int w)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BruceCoord(e, y, z, w));
    }

    [Fact]
    public void ToPhext_ReturnsCorrectFormat()
    {
        var coord = new BruceCoord(2, 3, 4, 5);
        Assert.Equal("1.1.1/1.1.2/3.4.5", coord.ToPhext());
    }

    [Fact]
    public void ToShort_ReturnsCorrectFormat()
    {
        var coord = new BruceCoord(2, 3, 4, 5);
        Assert.Equal("2.3.4.5", coord.ToShort());
    }

    [Theory]
    [InlineData("2.3.4.5", 2, 3, 4, 5)]
    [InlineData("1.1.1.1", 1, 1, 1, 1)]
    [InlineData("9.9.9.9", 9, 9, 9, 9)]
    public void Parse_ValidInput_ReturnsCoord(string input, int e, int y, int z, int w)
    {
        var coord = BruceCoord.Parse(input);
        
        Assert.Equal(e, coord.EntityType);
        Assert.Equal(y, coord.Y);
        Assert.Equal(z, coord.Z);
        Assert.Equal(w, coord.W);
    }

    [Theory]
    [InlineData("2.3.4", 2, 3, 4, 1)]  // Old format backwards compatibility
    public void Parse_OldFormat_ReturnsCoordWithW1(string input, int e, int y, int z, int w)
    {
        var coord = BruceCoord.Parse(input);
        
        Assert.Equal(e, coord.EntityType);
        Assert.Equal(y, coord.Y);
        Assert.Equal(z, coord.Z);
        Assert.Equal(w, coord.W);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("invalid")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4.5")]
    public void Parse_InvalidInput_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => BruceCoord.Parse(input));
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrueAndCoord()
    {
        var result = BruceCoord.TryParse("2.3.4.5", out var coord);
        
        Assert.True(result);
        Assert.Equal(2, coord.EntityType);
    }

    [Fact]
    public void TryParse_InvalidInput_ReturnsFalse()
    {
        var result = BruceCoord.TryParse("invalid", out _);
        Assert.False(result);
    }

    [Fact]
    public void LinearPosition_CalculatesCorrectly()
    {
        // Position 0 = (1,1,1)
        Assert.Equal(0, new BruceCoord(2, 1, 1, 1).LinearPosition);
        
        // Position 1 = (1,1,2)
        Assert.Equal(1, new BruceCoord(2, 1, 1, 2).LinearPosition);
        
        // Position 9 = (1,2,1)
        Assert.Equal(9, new BruceCoord(2, 1, 2, 1).LinearPosition);
        
        // Position 81 = (2,1,1)
        Assert.Equal(81, new BruceCoord(2, 2, 1, 1).LinearPosition);
        
        // Max position 728 = (9,9,9)
        Assert.Equal(728, new BruceCoord(2, 9, 9, 9).LinearPosition);
    }

    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 1, 1, 2)]
    [InlineData(9, 1, 2, 1)]
    [InlineData(81, 2, 1, 1)]
    [InlineData(728, 9, 9, 9)]
    public void FromLinear_ReturnsCorrectCoord(int pos, int y, int z, int w)
    {
        var coord = BruceCoord.FromLinear(2, pos);
        
        Assert.Equal(2, coord.EntityType);
        Assert.Equal(y, coord.Y);
        Assert.Equal(z, coord.Z);
        Assert.Equal(w, coord.W);
    }

    [Fact]
    public void FromLinear_InvalidPosition_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BruceCoord.FromLinear(2, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BruceCoord.FromLinear(2, 729));
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new BruceCoord(2, 3, 4, 5);
        var b = new BruceCoord(2, 3, 4, 5);
        
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new BruceCoord(2, 3, 4, 5);
        var b = new BruceCoord(2, 3, 4, 6);
        
        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void CompareTo_OrdersByEntityTypeThenPosition()
    {
        var a = new BruceCoord(1, 1, 1, 1);
        var b = new BruceCoord(2, 1, 1, 1);
        var c = new BruceCoord(2, 1, 1, 2);
        
        Assert.True(a < b);
        Assert.True(b < c);
        Assert.True(a < c);
    }

    [Fact]
    public void Next_ReturnsNextInSequence()
    {
        var coord = new BruceCoord(2, 1, 1, 1);
        var next = coord.Next();
        
        Assert.NotNull(next);
        Assert.Equal(new BruceCoord(2, 1, 1, 2), next.Value);
    }

    [Fact]
    public void Next_AtMax_ReturnsNull()
    {
        var coord = new BruceCoord(2, 9, 9, 9);
        Assert.Null(coord.Next());
    }

    [Fact]
    public void JsonSerialization_RoundTrips()
    {
        var coord = new BruceCoord(2, 3, 4, 5);
        var json = JsonSerializer.Serialize(coord);
        var restored = JsonSerializer.Deserialize<BruceCoord>(json);
        
        Assert.Equal(coord, restored);
        Assert.Contains("2.3.4.5", json);
    }
}

public class CoordAllocatorTests
{
    [Fact]
    public void Allocate_ReturnsSequentialCoords()
    {
        var allocator = new CoordAllocator();
        
        var first = allocator.Allocate(BruceCoord.Tasks);
        var second = allocator.Allocate(BruceCoord.Tasks);
        
        Assert.Equal(0, first.LinearPosition);
        Assert.Equal(1, second.LinearPosition);
    }

    [Fact]
    public void Allocate_DifferentEntityTypes_IndependentSequences()
    {
        var allocator = new CoordAllocator();
        
        var task = allocator.Allocate(BruceCoord.Tasks);
        var user = allocator.Allocate(BruceCoord.Users);
        
        Assert.Equal(BruceCoord.Tasks, task.EntityType);
        Assert.Equal(BruceCoord.Users, user.EntityType);
        Assert.Equal(0, task.LinearPosition);
        Assert.Equal(0, user.LinearPosition);
    }

    [Fact]
    public void SetHighWater_UpdatesNextAllocation()
    {
        var allocator = new CoordAllocator();
        allocator.SetHighWater(BruceCoord.Tasks, 10);
        
        var next = allocator.Allocate(BruceCoord.Tasks);
        
        Assert.Equal(11, next.LinearPosition);
    }

    [Fact]
    public void GetRemaining_ReturnsCorrectCount()
    {
        var allocator = new CoordAllocator();
        
        Assert.Equal(729, allocator.GetRemaining(BruceCoord.Tasks));
        
        allocator.Allocate(BruceCoord.Tasks);
        Assert.Equal(728, allocator.GetRemaining(BruceCoord.Tasks));
    }

    [Fact]
    public void Allocate_ThreadSafe()
    {
        var allocator = new CoordAllocator();
        var coords = new System.Collections.Concurrent.ConcurrentBag<BruceCoord>();
        
        Parallel.For(0, 100, _ =>
        {
            coords.Add(allocator.Allocate(BruceCoord.Tasks));
        });
        
        // All should be unique
        Assert.Equal(100, coords.Distinct().Count());
    }
}
