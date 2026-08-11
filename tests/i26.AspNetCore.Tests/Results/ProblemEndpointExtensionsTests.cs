using i26.AspNetCore.Results;
using i26.Core.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace i26.AspNetCore.Tests.Results;

public class ProblemEndpointExtensionsTests
{
    private static readonly Error NotFound = Error.NotFound("course.notFound");
    private static readonly Error AlreadyPublished = Error.Conflict("course.alreadyPublished");

    /// <summary>
    /// Builds a throwaway app, maps one endpoint through <paramref name="declare"/> and returns the
    /// problem responses the endpoint ended up advertising.
    /// </summary>
    private static IProducesResponseTypeMetadata[] DeclaredProblems(
        Func<RouteHandlerBuilder, RouteHandlerBuilder> declare)
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        declare(app.MapGet("courses", () => TypedResults.Ok()));

        return ProblemsOf(app);
    }

    private static IProducesResponseTypeMetadata[] ProblemsOf(WebApplication app) =>
        [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .SelectMany(endpoint => endpoint.Metadata.OfType<IProducesResponseTypeMetadata>())
            .Where(metadata => metadata.ContentTypes.Contains("application/problem+json"))];

    [Fact]
    public void It_declares_one_response_per_kind_of_failure()
    {
        var problems = DeclaredProblems(route =>
            route.ProducesProblem(ErrorType.NotFound, ErrorType.Conflict));

        Assert.Equal([404, 409], problems.Select(problem => problem.StatusCode));
    }

    [Fact]
    public void It_declares_problem_details_as_the_payload()
    {
        var problems = DeclaredProblems(route => route.ProducesProblem(ErrorType.NotFound));

        var problem = Assert.Single(problems);

        Assert.Equal(typeof(ProblemDetails), problem.Type);
        Assert.Equal(["application/problem+json"], problem.ContentTypes);
    }

    [Fact]
    public void It_takes_the_errors_themselves()
    {
        var problems = DeclaredProblems(route => route.ProducesProblem(NotFound, AlreadyPublished));

        Assert.Equal([404, 409], problems.Select(problem => problem.StatusCode));
    }

    [Fact]
    public void Kinds_of_failure_sharing_a_status_are_declared_once()
    {
        // Validation and Problem are both 400.
        var problems = DeclaredProblems(route =>
            route.ProducesProblem(ErrorType.Validation, ErrorType.Problem, ErrorType.NotFound));

        Assert.Equal([400, 404], problems.Select(problem => problem.StatusCode));
    }

    [Fact]
    public void Errors_sharing_a_status_are_declared_once()
    {
        var problems = DeclaredProblems(route =>
            route.ProducesProblem(NotFound, Error.NotFound("course.moduleNotFound")));

        Assert.Single(problems, problem => problem.StatusCode == 404);
    }

    [Fact]
    public void It_declares_the_status_the_error_actually_answers_with()
    {
        foreach (var type in Enum.GetValues<ErrorType>())
        {
            var problems = DeclaredProblems(route => route.ProducesProblem(type));

            var problem = Assert.Single(problems);

            Assert.Equal(new Error("some.code", type).StatusCode, problem.StatusCode);
        }
    }

    [Fact]
    public void It_works_on_a_group_and_reaches_every_endpoint_in_it()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        var group = app.MapGroup("v1").ProducesProblem(ErrorType.Unauthorized);
        group.MapGet("courses", () => TypedResults.Ok());
        group.MapGet("students", () => TypedResults.Ok());

        var problems = ProblemsOf(app);

        Assert.Equal(2, problems.Length);
        Assert.All(problems, problem => Assert.Equal(401, problem.StatusCode));
    }

    [Fact]
    public void It_returns_the_builder_it_was_given()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        var group = app.MapGroup("v1");

        Assert.Same(group, group.ProducesProblem(ErrorType.NotFound));
        Assert.Same(group, group.ProducesProblem(NotFound));
    }

    [Fact]
    public void It_leaves_the_success_response_of_the_endpoint_alone()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        app.MapGet("courses", () => TypedResults.Ok()).ProducesProblem(ErrorType.NotFound);

        var declared = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .SelectMany(endpoint => endpoint.Metadata.OfType<IProducesResponseTypeMetadata>())
            .Select(metadata => metadata.StatusCode)
            .ToArray();

        Assert.Contains(200, declared);
        Assert.Contains(404, declared);
    }
}
