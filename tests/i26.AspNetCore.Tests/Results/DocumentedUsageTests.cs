using i26.AspNetCore.Results;
using i26.Core.Results;
using Microsoft.AspNetCore.Http;

namespace i26.AspNetCore.Tests.Results;

// Declared inside the namespace on purpose. This file sits in a namespace whose parent has a
// member named Results, and an alias placed above the namespace declaration lives at
// compilation-unit scope, which loses to that member. Inside, it wins.
using Results = Microsoft.AspNetCore.Http.Results;

/// <summary>
/// Locks the shape the README documents. It compiling is most of the test: folding a result with
/// two method groups only works while both sides answer <see cref="IResult"/>.
/// </summary>
public class DocumentedUsageTests
{
    private static Task<Result<int>> HandleAsync(bool succeed) =>
        Task.FromResult(succeed ? Result.Ok(42) : Result.Failure<int>(Error.NotFound("course.notFound")));

    [Fact]
    public void Match_folds_a_result_with_two_method_groups()
    {
        Result<int> failed = Error.NotFound("course.notFound");

        IResult problem = failed.Match(Results.Ok, ProblemResults.Problem);
        IResult ok = Result.Ok(42).Match(Results.Ok, ProblemResults.Problem);

        Assert.NotNull(problem);
        Assert.NotNull(ok);
    }

    [Fact]
    public async Task An_asynchronous_chain_ends_in_the_same_fold()
    {
        IResult result = await HandleAsync(succeed: true)
            .EnsureAsync(value => value > 0, Error.Conflict("course.alreadyPublished"))
            .BindAsync(value => Task.FromResult(Result.Ok(value + 1)))
            .MapAsync(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .MatchAsync(Results.Ok, ProblemResults.Problem);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task A_result_without_a_value_needs_a_lambda_on_the_success_side()
    {
        // Results.Ok takes an optional parameter, and a method group with one does not convert to
        // Func<IResult>. With a value there is a parameter to bind, so the method group works.
        IResult result = await Task.FromResult(Result.Ok())
            .MatchAsync(() => Results.Ok(), ProblemResults.Problem);

        Assert.NotNull(result);
    }
}
