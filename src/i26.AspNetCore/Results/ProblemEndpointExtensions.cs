using i26.Core.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace i26.AspNetCore.Results;

/// <summary>Declares, in the API description, the problem responses an endpoint can answer with.</summary>
/// <remarks>
/// Taking the errors themselves keeps the OpenAPI document following the code, instead of a
/// hand-written <c>ProducesProblem(404)</c> that rots. Statuses are deduplicated.
/// </remarks>
public static class ProblemEndpointExtensions
{
    /// <summary>Media type of a problem response, per RFC 9457.</summary>
    private static readonly string[] ProblemContentTypes = ["application/problem+json"];

    /// <summary>Declares the problem responses matching the given kinds of failure.</summary>
    /// <remarks>Works on a route group as well as on a single endpoint.</remarks>
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
    /// <remarks>
    /// The one to reach for: the endpoint names the errors its handler returns, straight from the
    /// <c>{Entity}Errors</c> class, instead of restating their statuses.
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

    // One response entry per status. The payload is ProblemDetails even for a validation failure:
    // what goes out is a problem document with the errors in an extension, not the
    // HttpValidationProblemDetails shape of ProducesValidationProblem.
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
