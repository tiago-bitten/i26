using i26.AspNetCore.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace i26.AspNetCore.Tests.Endpoints;

internal sealed class GetCourseEndpoint : IEndpoint
{
    public const string Route = "courses/{id}";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, () => TypedResults.Ok());
}

internal sealed class CreateCourseEndpoint : IEndpoint
{
    public const string Route = "courses";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, () => TypedResults.Created(Route));
}

/// <summary>Proves the implementations come out of the container, not out of an <c>Activator</c>.</summary>
internal sealed class DependentEndpoint(EndpointDependency dependency) : IEndpoint
{
    public const string Route = "dependency";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, () => TypedResults.Text(dependency.Value));
}

internal sealed class EndpointDependency
{
    public string Value => "injected";
}

/// <summary>Must be skipped by the scan: there is nothing to instantiate.</summary>
internal abstract class AbstractEndpoint : IEndpoint
{
    public abstract void MapEndpoint(IEndpointRouteBuilder app);
}

/// <summary>Must be skipped by the scan: an open generic cannot be constructed.</summary>
internal sealed class OpenGenericEndpoint<TState> : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(typeof(TState).Name, () => TypedResults.Ok());
}
