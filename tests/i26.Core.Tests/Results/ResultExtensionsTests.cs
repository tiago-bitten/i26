using i26.Core.Results;

namespace i26.Core.Tests.Results;

public class ResultExtensionsTests
{
    private static readonly Error NotFound = Error.NotFound("course.notFound");
    private static readonly Error Conflict = Error.Conflict("course.alreadyPublished");

    [Fact]
    public void Match_runs_the_success_branch()
    {
        Assert.Equal("ok", Result.Ok().Match(() => "ok", failed => failed.Error.Code));
    }

    [Fact]
    public void Match_runs_the_failure_branch()
    {
        Assert.Equal(
            "course.notFound",
            Result.Failure(NotFound).Match(() => "ok", failed => failed.Error.Code));
    }

    [Fact]
    public void Match_of_a_typed_result_receives_the_value()
    {
        Assert.Equal(43, Result.Ok(42).Match(value => value + 1, _ => -1));
    }

    [Fact]
    public void Map_transforms_the_value()
    {
        var result = Result.Ok(2).Map(value => value * 21);

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Map_short_circuits_on_failure()
    {
        var called = false;

        Result<int> failed = NotFound;
        var result = failed.Map(value =>
        {
            called = true;
            return value;
        });

        Assert.False(called);
        Assert.Equal(NotFound, result.Error);
    }

    [Fact]
    public void Bind_chains_the_next_operation()
    {
        var result = Result.Ok(2).Bind(value => Result.Ok(value * 21));

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Bind_carries_the_failure_of_the_next_operation()
    {
        var result = Result.Ok(2).Bind(_ => Result.Failure<int>(Conflict));

        Assert.Equal(Conflict, result.Error);
    }

    [Fact]
    public void Bind_short_circuits_and_keeps_the_first_error()
    {
        var called = false;

        Result<int> failed = NotFound;
        var result = failed.Bind(_ =>
        {
            called = true;
            return Result.Failure<int>(Conflict);
        });

        Assert.False(called);
        Assert.Equal(NotFound, result.Error);
    }

    [Fact]
    public void Bind_bridges_between_typed_and_untyped_results()
    {
        Assert.True(Result.Ok(1).Bind(_ => Result.Ok()).IsSuccess);
        Assert.Equal(42, Result.Ok().Bind(() => Result.Ok(42)).Value);
        Assert.Equal(NotFound, Result.Failure(NotFound).Bind(() => Result.Ok(42)).Error);
    }

    [Fact]
    public void Tap_runs_only_on_success_and_passes_the_result_through()
    {
        var seen = 0;

        var success = Result.Ok(42).Tap(value => seen = value);
        Assert.Equal(42, seen);
        Assert.True(success.IsSuccess);

        seen = 0;
        Result<int> failed = NotFound;
        var passed = failed.Tap(value => seen = value);

        Assert.Equal(0, seen);
        Assert.Equal(NotFound, passed.Error);
    }

    [Fact]
    public void Ensure_fails_a_success_that_does_not_hold_the_condition()
    {
        var result = Result.Ok(1).Ensure(value => value > 10, Conflict);

        Assert.True(result.IsFailure);
        Assert.Equal(Conflict, result.Error);
    }

    [Fact]
    public void Ensure_keeps_a_success_that_holds_the_condition()
    {
        var result = Result.Ok(42).Ensure(value => value > 10, Conflict);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Ensure_does_not_evaluate_the_condition_on_a_failure()
    {
        var called = false;

        Result<int> failed = NotFound;
        var result = failed.Ensure(
            _ =>
            {
                called = true;
                return true;
            },
            Conflict);

        Assert.False(called);
        Assert.Equal(NotFound, result.Error);
    }

    [Fact]
    public void A_whole_chain_reads_top_to_bottom()
    {
        var published = Result.Ok(new Course("Algebra", IsPublished: false))
            .Ensure(course => !course.IsPublished, Conflict)
            .Map(course => course with { IsPublished = true })
            .Bind(course => course.Title.Length > 0 ? Result.Ok(course) : Result.Failure<Course>(NotFound));

        Assert.True(published.Value.IsPublished);
    }

    private sealed record Course(string Title, bool IsPublished);
}
