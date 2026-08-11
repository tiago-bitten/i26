using i26.Core.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace i26.AspNetCore.Results;

/// <summary>
/// Declares, in the API description, the problem responses an endpoint can answer with.
/// </summary>
/// <remarks>
/// <para>
/// It closes the loop with the result pattern: the handler already says which errors it can return,
/// and this puts the same statuses in the OpenAPI document, so the client sees them without anyone
/// hand-writing <c>ProducesProblem(404)</c> and letting it rot.
/// </para>
/// <code>
/// app.MapPost("courses/{id}/publish", Handle)
///     .ProducesProblem(CourseErrors.NotFound, CourseErrors.AlreadyPublished);
/// </code>
/// <para>
/// Statuses are deduplicated, so declaring two errors that share one — a
/// <see cref="ErrorType.Validation"/> and a <see cref="ErrorType.Problem"/>, both 400 — produces a
/// single response entry.
/// </para>
/// </remarks>
public static class ProblemEndpointExtensions
{
    /// <summary>Media type of a problem response, per RFC 9457.</summary>
    private static readonly string[] ProblemContentTypes = ["application/problem+json"];

    /// <summary>Declares the problem responses matching the given kinds of failure.</summary>
    /// <typeparam name="TBuilder">The endpoint or group builder.</typeparam>
    /// <param name="builder">What to declare the responses on.</param>
    /// <param name="errorType">The kind of failure the endpoint can answer with.</param>
    /// <param name="others">Any further kinds of failure.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="others"/> is <see langword="null"/>.
    /// </exception>
    public static TBuilder ProducesProblem<TBuilder>(
        this TBuilder builder,
        ErrorType errorType,
        params ErrorType[] others)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(others);

        var statusCodes = new SortedSet<int> { errorType.ToStatusCode() };

        foreach (var other in others)
        {
            statusCodes.Add(other.ToStatusCode());
        }

        return Declare(builder, statusCodes);
    }

    /// <summary>Declares the problem responses matching the given errors.</summary>
    /// <typeparam name="TBuilder">The endpoint or group builder.</typeparam>
    /// <param name="builder">What to declare the responses on.</param>
    /// <param name="error">An error the endpoint can answer with.</param>
    /// <param name="others">Any further errors.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// The overload to reach for in practice: the endpoint names the very errors its handler
    /// returns, straight from the <c>{Entity}Errors</c> class, instead of restating their statuses.
    /// </remarks>
    public static TBuilder ProducesProblem<TBuilder>(
        this TBuilder builder,
        Error error,
        params Error[] others)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(others);

        var statusCodes = new SortedSet<int> { error.StatusCode };

        foreach (var other in others)
        {
            ArgumentNullException.ThrowIfNull(other);
            statusCodes.Add(other.StatusCode);
        }

        return Declare(builder, statusCodes);
    }

    /// <summary>
    /// Adds one response entry per status.
    /// </summary>
    /// <remarks>
    /// The declared payload is <see cref="ProblemDetails"/> even for a validation failure: what goes
    /// out is a problem document with the individual errors in an <c>errors</c> extension, not the
    /// <c>HttpValidationProblemDetails</c> shape of <c>ProducesValidationProblem</c>.
    /// </remarks>
    private static TBuilder Declare<TBuilder>(TBuilder builder, SortedSet<int> statusCodes)
        where TBuilder : IEndpointConventionBuilder
    {
        foreach (var statusCode in statusCodes)
        {
            builder.WithMetadata(
                new ProducesResponseTypeMetadata(statusCode, typeof(ProblemDetails), ProblemContentTypes));
        }

        return builder;
    }
}
