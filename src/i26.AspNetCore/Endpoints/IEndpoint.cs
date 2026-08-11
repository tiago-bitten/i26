using Microsoft.AspNetCore.Routing;

namespace i26.AspNetCore.Endpoints;

/// <summary>
/// One endpoint, declaring its own route. Implementations are discovered by
/// <see cref="EndpointExtensions.AddEndpoints"/> and mapped by
/// <see cref="EndpointExtensions.MapEndpoints"/>.
/// </summary>
/// <remarks>
/// <para>
/// It keeps the route, the handler and everything the route needs — authorization, tags, rate
/// limit — in the same file, instead of piling every <c>MapGet</c> in the world into
/// <c>Program.cs</c>:
/// </para>
/// <code>
/// internal sealed class GetCourse : IEndpoint
/// {
///     public void MapEndpoint(IEndpointRouteBuilder app)
///     {
///         app.MapGet("courses/{id}", async (
///                 [FromRoute] CourseId id,
///                 [FromServices] IQueryHandler&lt;GetCourseQuery, CourseResponse&gt; handler,
///                 CancellationToken ct) =>
///             {
///                 var result = await handler.HandleAsync(new GetCourseQuery(id), ct);
///
///                 return result.Match(Results.Ok, ProblemResults.Problem);
///             })
///             .RequireAuthorization()
///             .WithTags("Courses");
///     }
/// }
/// </code>
/// <para>
/// Implementations are resolved from the container, so they can take constructor dependencies —
/// though needing one usually means the dependency belongs in the handler instead.
/// </para>
/// </remarks>
public interface IEndpoint
{
    /// <summary>Declares the route on the given builder.</summary>
    /// <param name="app">
    /// Where the route is mapped — the application itself, or the group
    /// <see cref="EndpointExtensions.MapEndpoints"/> was called on.
    /// </param>
    void MapEndpoint(IEndpointRouteBuilder app);
}
