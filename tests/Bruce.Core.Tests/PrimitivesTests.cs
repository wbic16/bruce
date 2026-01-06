using Bruce.Core.Primitives;
using Xunit;

namespace Bruce.Core.Tests;

public class ResultTests
{
    [Fact]
    public void Ok_CreatesSuccessResult()
    {
        var result = Result<int>.Ok(42);
        
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Fail_CreatesFailureResult()
    {
        var result = Result<int>.Fail("Something went wrong", ResultErrorCode.ValidationFailed);
        
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("Something went wrong", result.Error);
        Assert.Equal(ResultErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public void Value_OnFailure_ThrowsInvalidOperationException()
    {
        var result = Result<int>.Fail("error");
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Error_OnSuccess_ThrowsInvalidOperationException()
    {
        var result = Result<int>.Ok(42);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        var result = Result<int>.Ok(42);
        var mapped = result.Map(x => x.ToString());
        
        Assert.True(mapped.IsSuccess);
        Assert.Equal("42", mapped.Value);
    }

    [Fact]
    public void Map_OnFailure_PropagatesError()
    {
        var result = Result<int>.Fail("error", ResultErrorCode.NotFound);
        var mapped = result.Map(x => x.ToString());
        
        Assert.True(mapped.IsFailure);
        Assert.Equal("error", mapped.Error);
        Assert.Equal(ResultErrorCode.NotFound, mapped.ErrorCode);
    }

    [Fact]
    public void Bind_OnSuccess_ChainsResults()
    {
        var result = Result<int>.Ok(42);
        var bound = result.Bind(x => x > 0 
            ? Result<string>.Ok("positive") 
            : Result<string>.Fail("not positive"));
        
        Assert.True(bound.IsSuccess);
        Assert.Equal("positive", bound.Value);
    }

    [Fact]
    public void Match_CallsCorrectAction()
    {
        var successCalled = false;
        var failureCalled = false;
        
        Result<int>.Ok(42).Match(
            _ => successCalled = true,
            (_, _) => failureCalled = true
        );
        
        Assert.True(successCalled);
        Assert.False(failureCalled);
    }

    [Fact]
    public void GetValueOrDefault_ReturnsValueOnSuccess()
    {
        var result = Result<int>.Ok(42);
        Assert.Equal(42, result.GetValueOrDefault(0));
    }

    [Fact]
    public void GetValueOrDefault_ReturnsDefaultOnFailure()
    {
        var result = Result<int>.Fail("error");
        Assert.Equal(0, result.GetValueOrDefault(0));
    }

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccess()
    {
        Result<int> result = 42;
        
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Theory]
    [InlineData(ResultErrorCode.NotFound)]
    [InlineData(ResultErrorCode.ValidationFailed)]
    [InlineData(ResultErrorCode.Conflict)]
    [InlineData(ResultErrorCode.CapacityExceeded)]
    public void FactoryMethods_SetCorrectErrorCode(ResultErrorCode code)
    {
        var result = code switch
        {
            ResultErrorCode.NotFound => Result<int>.NotFound("msg"),
            ResultErrorCode.ValidationFailed => Result<int>.Invalid("msg"),
            ResultErrorCode.Conflict => Result<int>.Conflict("msg"),
            ResultErrorCode.CapacityExceeded => Result<int>.CapacityExceeded("msg"),
            _ => Result<int>.Fail("msg", code)
        };
        
        Assert.Equal(code, result.ErrorCode);
    }
}

public class NonGenericResultTests
{
    [Fact]
    public void Ok_CreatesSuccessResult()
    {
        var result = Result.Ok();
        
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void Fail_CreatesFailureResult()
    {
        var result = Result.Fail("error", ResultErrorCode.Unknown);
        
        Assert.True(result.IsFailure);
        Assert.Equal("error", result.Error);
    }

    [Fact]
    public void Map_OnSuccess_CreatesGenericResult()
    {
        var result = Result.Ok();
        var mapped = result.Map(() => 42);
        
        Assert.True(mapped.IsSuccess);
        Assert.Equal(42, mapped.Value);
    }
}

public class GuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NotNullOrEmpty_InvalidInput_ReturnsFailure(string? input)
    {
        var result = Guard.NotNullOrEmpty(input);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void NotNullOrEmpty_ValidInput_ReturnsSuccess()
    {
        var result = Guard.NotNullOrEmpty("hello");
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotNullOrWhitespace_InvalidInput_ReturnsFailure(string? input)
    {
        var result = Guard.NotNullOrWhitespace(input);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Length_WithinBounds_ReturnsSuccess()
    {
        var result = Guard.Length("hello", 1, 10);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Length_TooShort_ReturnsFailure()
    {
        var result = Guard.Length("hi", 5, 10);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Length_TooLong_ReturnsFailure()
    {
        var result = Guard.Length("hello world", 1, 5);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void InRange_WithinBounds_ReturnsSuccess()
    {
        var result = Guard.InRange(5, 1, 10);
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void InRange_OutOfBounds_ReturnsFailure()
    {
        var result = Guard.InRange(15, 1, 10);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Condition_True_ReturnsSuccess()
    {
        var result = Guard.Condition(true, "error");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Condition_False_ReturnsFailure()
    {
        var result = Guard.Condition(false, "error");
        Assert.True(result.IsFailure);
        Assert.Equal("error", result.Error);
    }

    [Fact]
    public void ValidTaskId_ValidFormat_ReturnsSuccess()
    {
        var result = Guard.ValidTaskId("T-abc123-xyz");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidTaskId_InvalidFormat_ReturnsFailure()
    {
        var result = Guard.ValidTaskId("invalid-id");
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ValidWorkerId_ValidFormat_ReturnsSuccess()
    {
        var result = Guard.ValidWorkerId("W-abc123-xyz");
        Assert.True(result.IsSuccess);
    }
}

public class ValidationBuilderTests
{
    [Fact]
    public void Build_NoErrors_ReturnsSuccess()
    {
        var builder = new ValidationBuilder();
        builder.Validate(Result.Ok());
        builder.Validate(Result<int>.Ok(42));
        
        var result = builder.Build();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Build_WithErrors_ReturnsAggregatedFailure()
    {
        var builder = new ValidationBuilder();
        builder.Validate(Result.Fail("error 1"));
        builder.Validate(Result<int>.Fail("error 2"));
        
        var result = builder.Build();
        Assert.True(result.IsFailure);
        Assert.Contains("error 1", result.Error);
        Assert.Contains("error 2", result.Error);
    }

    [Fact]
    public void Build_WithValue_ReturnsSuccessWithValue()
    {
        var builder = new ValidationBuilder();
        builder.Validate(Result.Ok());
        
        var result = builder.Build(() => 42);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }
}

public class IdGenTests
{
    [Fact]
    public void Task_StartsWithT()
    {
        var id = IdGen.Task();
        Assert.StartsWith("T-", id);
    }

    [Fact]
    public void Worker_StartsWithW()
    {
        var id = IdGen.Worker();
        Assert.StartsWith("W-", id);
    }

    [Fact]
    public void Message_StartsWithM()
    {
        var id = IdGen.Message();
        Assert.StartsWith("M-", id);
    }

    [Fact]
    public void Artifact_StartsWithF()
    {
        var id = IdGen.Artifact();
        Assert.StartsWith("F-", id);
    }

    [Fact]
    public void GeneratedIds_AreUnique()
    {
        var ids = Enumerable.Range(0, 1000).Select(_ => IdGen.Task()).ToList();
        Assert.Equal(1000, ids.Distinct().Count());
    }

    [Fact]
    public void GetEntityType_ReturnsCorrectType()
    {
        Assert.Equal("Task", IdGen.GetEntityType("T-abc123-xyz"));
        Assert.Equal("Worker", IdGen.GetEntityType("W-abc123-xyz"));
        Assert.Equal("Message", IdGen.GetEntityType("M-abc123-xyz"));
        Assert.Equal("Artifact", IdGen.GetEntityType("F-abc123-xyz"));
        Assert.Null(IdGen.GetEntityType("X-abc123"));
        Assert.Null(IdGen.GetEntityType("invalid"));
    }

    [Fact]
    public void IsValidTaskId_ValidatesCorrectly()
    {
        Assert.True(IdGen.IsValidTaskId("T-abc123-xyz"));
        Assert.False(IdGen.IsValidTaskId("W-abc123-xyz"));
        Assert.False(IdGen.IsValidTaskId("invalid"));
        Assert.False(IdGen.IsValidTaskId(null));
    }
}
