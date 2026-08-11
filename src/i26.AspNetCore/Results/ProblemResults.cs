using i26.Core.Results;
using Microsoft.AspNetCore.Http;

namespace i26.AspNetCore.Results;

/// <summary>
/// Turns a failed <see cref="Result"/> into an RFC 9457 <c>application/problem+json</c> response.
/// </summary>
/// <remarks>
/// Written to be usable as a method group, which is what keeps the endpoint down to one line:
/// <code>
/// var result = await handler.HandleAsync(command, ct);
/// return result.Match(Results.Ok, ProblemResults.Problem);
/// </code>
/// The description is resolved when the response is executed, from the
/// <see cref="IErrorTranslator"/> registered in the request's services — so there is no static
/// state to configure and nothing to inject into the endpoint.
/// </remarks>
public static class ProblemResults
{
    /// <summary>Builds the problem response for a failed result.</summary>
    /// <param name="result">The failed result.</param>
    /// <returns>An <see cref="IResult"/> that writes the problem details.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="result"/> is a success.</exception>
    public static IResult Problem(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            throw new InvalidOperationException(
                "Only a failed result can be turned into a problem response.");
        }

        return new ProblemResult(result.Error);
    }

    /// <summary>Builds the problem response for an error.</summary>
    /// <param name="error">The failure to describe.</param>
    /// <returns>An <see cref="IResult"/> that writes the problem details.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static IResult Problem(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new ProblemResult(error);
    }

    /// <summary>The specification section defining a status code.</summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>
    /// The URI used as the <c>type</c> member of the problem details, or <see langword="null"/> for
    /// a status with no definition to point at — in which case the member is left out and the
    /// client falls back to <c>about:blank</c>, as RFC 9457 prescribes.
    /// </returns>
    /// <remarks>
    /// Keyed by status code rather than by <see cref="ErrorType"/>: the status is what the
    /// specification section actually describes, and it keeps this from becoming a second copy of
    /// the mapping in <see cref="Error.StatusCode"/>. Most codes live in RFC 9110; the ones that do
    /// not point at the RFC that introduced them.
    /// </remarks>
    public static string? GetTypeUri(int statusCode) => statusCode switch
    {
        400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        401 => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        402 => "https://tools.ietf.org/html/rfc9110#section-15.5.3",
        403 => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        404 => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        405 => "https://tools.ietf.org/html/rfc9110#section-15.5.6",
        406 => "https://tools.ietf.org/html/rfc9110#section-15.5.7",
        407 => "https://tools.ietf.org/html/rfc9110#section-15.5.8",
        408 => "https://tools.ietf.org/html/rfc9110#section-15.5.9",
        409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        410 => "https://tools.ietf.org/html/rfc9110#section-15.5.11",
        411 => "https://tools.ietf.org/html/rfc9110#section-15.5.12",
        412 => "https://tools.ietf.org/html/rfc9110#section-15.5.13",
        413 => "https://tools.ietf.org/html/rfc9110#section-15.5.14",
        414 => "https://tools.ietf.org/html/rfc9110#section-15.5.15",
        415 => "https://tools.ietf.org/html/rfc9110#section-15.5.16",
        416 => "https://tools.ietf.org/html/rfc9110#section-15.5.17",
        417 => "https://tools.ietf.org/html/rfc9110#section-15.5.18",
        421 => "https://tools.ietf.org/html/rfc9110#section-15.5.20",
        422 => "https://tools.ietf.org/html/rfc9110#section-15.5.21",
        423 => "https://tools.ietf.org/html/rfc4918#section-11.3",
        424 => "https://tools.ietf.org/html/rfc4918#section-11.4",
        425 => "https://tools.ietf.org/html/rfc8470#section-5.2",
        426 => "https://tools.ietf.org/html/rfc9110#section-15.5.22",
        428 => "https://tools.ietf.org/html/rfc6585#section-3",
        429 => "https://tools.ietf.org/html/rfc6585#section-4",
        431 => "https://tools.ietf.org/html/rfc6585#section-5",
        451 => "https://tools.ietf.org/html/rfc7725#section-3",

        500 => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        501 => "https://tools.ietf.org/html/rfc9110#section-15.6.2",
        502 => "https://tools.ietf.org/html/rfc9110#section-15.6.3",
        503 => "https://tools.ietf.org/html/rfc9110#section-15.6.4",
        504 => "https://tools.ietf.org/html/rfc9110#section-15.6.5",
        505 => "https://tools.ietf.org/html/rfc9110#section-15.6.6",
        506 => "https://tools.ietf.org/html/rfc2295#section-8.1",
        507 => "https://tools.ietf.org/html/rfc4918#section-11.5",
        508 => "https://tools.ietf.org/html/rfc5842#section-7.2",
        510 => "https://tools.ietf.org/html/rfc2774#section-7",
        511 => "https://tools.ietf.org/html/rfc6585#section-6",

        _ => null,
    };
}
