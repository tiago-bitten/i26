using i26.Core.Results;

namespace i26.Core.Tests.Results;

public class ResultAsyncExtensionsTests
{
    private static readonly Error NotFound = Error.NotFound("course.notFound");
    private static readonly Error Conflict = Error.Conflict("course.alreadyPublished");

    private static Task<Result> OkAsync() => Task.FromResult(Result.Ok());

    private static Task<Result<int>> OkAsync(int value) => Task.FromResult(Result.Ok(value));

    private static Task<Result<int>> FailedAsync() => Task.FromResult(Result.Failure<int>(NotFound));

    [Fact]
    public async Task MatchAsync_folds_a_pending_result()
    {
        Assert.Equal("ok", await OkAsync().MatchAsync(() => "ok", failed => failed.Error.Code));
        Assert.Equal(43, await OkAsync(42).MatchAsync(value => value + 1, _ => -1));
        Assert.Equal("course.notFound", await FailedAsync().MatchAsync(_ => "ok", failed => failed.Error.Code));
    }

    [Fact]
    public async Task MapAsync_transforms_a_pending_value()
    {
        var result = await OkAsync(2).MapAsync(value => value * 21);

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task MapAsync_transforms_with_an_asynchronous_projection()
    {
        var result = await Result.Ok(2).MapAsync(value => Task.FromResult(value * 21));

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task MapAsync_short_circuits_on_failure()
    {
        var called = false;

        Result<int> failed = NotFound;
        var result = await failed.MapAsync(value =>
        {
            called = true;
            return Task.FromResult(value);
        });

        Assert.False(called);
        Assert.Equal(NotFound, result.Error);
    }

    [Fact]
    public async Task BindAsync_chains_an_asynchronous_step()
    {
        var result = await Result.Ok(2).BindAsync(value => OkAsync(value * 21));

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task BindAsync_chains_from_a_pending_result()
    {
        Assert.Equal(42, (await OkAsync(2).BindAsync(value => OkAsync(value * 21))).Value);
        Assert.Equal(42, (await OkAsync(2).BindAsync(value => Result.Ok(value * 21))).Value);
    }

    [Fact]
    public async Task BindAsync_short_circuits_and_keeps_the_first_error()
    {
        var called = false;

        var result = await FailedAsync().BindAsync(_ =>
        {
            called = true;
            return OkAsync(1);
        });

        Assert.False(called);
        Assert.Equal(NotFound, result.Error);
    }

    [Fact]
    public async Task BindAsync_bridges_to_an_untyped_result()
    {
        Assert.True((await Result.Ok(1).BindAsync(_ => OkAsync())).IsSuccess);
        Assert.Equal(NotFound, (await Result.Failure(NotFound).BindAsync(OkAsync)).Error);
    }

    [Fact]
    public async Task TapAsync_runs_only_on_success()
    {
        var seen = 0;

        await Result.Ok(42).TapAsync(value =>
        {
            seen = value;
            return Task.CompletedTask;
        });

        Assert.Equal(42, seen);

        seen = 0;
        Result<int> failed = NotFound;
        var passed = await failed.TapAsync(value =>
        {
            seen = value;
            return Task.CompletedTask;
        });

        Assert.Equal(0, seen);
        Assert.Equal(NotFound, passed.Error);
    }

    [Fact]
    public async Task TapAsync_runs_a_synchronous_effect_on_a_pending_result()
    {
        var seen = 0;

        var result = await OkAsync(42).TapAsync(value => seen = value);

        Assert.Equal(42, seen);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureAsync_fails_a_pending_success_that_does_not_hold_the_condition()
    {
        var result = await OkAsync(1).EnsureAsync(value => value > 10, Conflict);

        Assert.Equal(Conflict, result.Error);
    }

    [Fact]
    public async Task A_whole_asynchronous_chain_reads_top_to_bottom()
    {
        var result = await OkAsync(2)
            .EnsureAsync(value => value > 0, Conflict)
            .BindAsync(value => OkAsync(value * 21))
            .MapAsync(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal("42", result.Value);
    }
}
