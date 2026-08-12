using i26.Core.Results;
using Microsoft.AspNetCore.Http;

namespace i26.AspNetCore.Results;

/// <summary>
/// Turns a failed <see cref="Result"/> into an RFC 9457 <c>application/problem+json</c> response.
/// </summary>
/// <remarks>
/// Usable as a method group, so an endpoint ends in
/// <c>result.Match(Results.Ok, ProblemResults.Problem)</c>. The description is resolved when the
/// response executes, from the <see cref="IErrorTranslator"/> in the request's services — no static
/// state, nothing to inject into the endpoint.
/// </remarks>
public static class ProblemResults
{
    /// <summary>Builds the problem response for a failed result.</summary>
    /// <exception cref="InvalidOperationException">The result is a success.</exception>
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
    public static IResult Problem(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new ProblemResult(error);
    }

    /// <summary>
    /// The specification section defining a status code, used as the <c>type</c> member. Null for a
    /// status with nothing to point at, which leaves the member out.
    /// </summary>
    /// <remarks>
    /// Keyed by status rather than by <see cref="ErrorType"/>, so this does not become a second copy
    /// of the mapping in <see cref="Error.StatusCode"/>.
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
