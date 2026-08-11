using i26.Core.Results;

namespace i26.Core.Tests.Results;

public class ResultTests
{
    private static readonly Error NotFound = Error.NotFound("course.notFound");

    [Fact]
    public void Ok_is_a_success_carrying_no_error()
    {
        var result = Result.Ok();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public void Ok_with_a_value_carries_it()
    {
        var result = Result.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public void Failure_carries_the_error_and_its_status()
    {
        var result = Result.Failure(NotFound);

        Assert.True(result.IsFailure);
        Assert.Equal(NotFound, result.Error);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public void An_error_converts_implicitly_into_a_failed_result()
    {
        Result result = NotFound;
        Result<int> typed = NotFound;

        Assert.True(result.IsFailure);
        Assert.True(typed.IsFailure);
        Assert.Equal(NotFound, typed.Error);
        Assert.Equal(404, typed.StatusCode);
    }

    [Fact]
    public void A_value_converts_implicitly_into_a_successful_result()
    {
        Result<string> result = "done";

        Assert.True(result.IsSuccess);
        Assert.Equal("done", result.Value);
    }

    [Fact]
    public void Value_throws_when_the_result_is_a_failure()
    {
        Result<int> result = NotFound;

        var exception = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("not available", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_success_cannot_carry_an_error()
    {
        Assert.Throws<ArgumentException>(() => new Result<int>(true, 1, NotFound));
    }

    [Fact]
    public void A_failure_cannot_carry_Error_None()
    {
        Assert.Throws<ArgumentException>(() => new Result<int>(false, 0, Error.None));
    }

    [Fact]
    public void ValidationFailure_builds_a_failed_result()
    {
        var result = Result<int>.ValidationFailure(ValidationError.FromResults([Result.Failure(NotFound)]));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void A_typed_result_is_usable_where_a_plain_result_is_expected()
    {
        Result result = Result.Ok(1);

        Assert.True(result.IsSuccess);
    }
}
