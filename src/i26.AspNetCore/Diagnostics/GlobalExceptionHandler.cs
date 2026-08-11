using System.Text.Json;
using i26.AspNetCore.Results;
using i26.Core.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace i26.AspNetCore.Diagnostics;

/// <summary>
/// Last line of defence: turns anything that escaped a handler into the same problem response a
/// failed <see cref="Result"/> would have produced.
/// </summary>
/// <remarks>
/// <para>Registration:</para>
/// <code>
/// builder.Services.AddProblemDetails();
/// builder.Services.AddExceptionHandler&lt;GlobalExceptionHandler&gt;();
/// ...
/// app.UseExceptionHandler();
/// </code>
/// <para>
/// It handles two things. A <see cref="BadHttpRequestException"/> — malformed JSON, a route value
/// that would not bind — is the caller's fault and comes back as 400 naming the offending field,
/// so endpoints never have to check for it. Anything else is a bug and comes back as 500.
/// </para>
/// <para>
/// The response goes through the same renderer as <see cref="ProblemResults"/>: same
/// <c>application/problem+json</c> media type, same <c>code</c> extension, same
/// <see cref="IErrorTranslator"/>. A client parses a crash exactly like it parses a business
/// failure.
/// </para>
/// </remarks>
/// <param name="logger">Where the exception is recorded.</param>
/// <param name="environment">
/// Decides whether the exception message may reach the client — see
/// <see cref="TryHandleAsync"/>.
/// </param>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    /// <summary>Code answered when the request body could not be read at all.</summary>
    public const string InvalidBodyCode = "request.body.invalid";

    /// <summary>Code answered when nothing else matched — an actual bug.</summary>
    public const string FailureCode = "general.failure";

    /// <summary>Prefix of the code answered when one field of the body could not be read.</summary>
    /// <remarks>The full code is <c>request.{field}.invalid</c>, with the field as an argument.</remarks>
    public const string InvalidFieldCodePrefix = "request.";

    /// <summary>Writes the problem response for an unhandled exception.</summary>
    /// <param name="httpContext">The request being answered.</param>
    /// <param name="exception">What escaped.</param>
    /// <param name="cancellationToken">Unused: the response is written through the result pipeline.</param>
    /// <returns><see langword="true"/> when the response was written.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="httpContext"/> or <paramref name="exception"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// The exception message reaches the client only in the Development environment. Anywhere else
    /// a 500 carries nothing but its code: messages routinely spell out connection strings, file
    /// paths and SQL, and none of that is the caller's business. The full exception is always in the
    /// log either way.
    /// </remarks>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (httpContext.Response.HasStarted)
        {
            // Half of the response is already on the wire; there is no problem document to write.
            logger.LogWarning(exception, "Unhandled exception after the response had started");
            return false;
        }

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The caller hung up. Nobody is listening, and it is not a failure worth alerting on.
            logger.LogDebug(exception, "Request aborted by the caller");
            return true;
        }

        var (error, detail) = Describe(exception);

        if (error.StatusCode >= 500)
        {
            logger.LogError(exception, "Unhandled exception answered as {Code}", error.Code);
        }
        else
        {
            logger.LogInformation("Malformed request answered as {Code}: {Message}", error.Code, exception.Message);
        }

        await new ProblemResult(error, detail).ExecuteAsync(httpContext).ConfigureAwait(false);

        return true;
    }

    private (Error Error, string? Detail) Describe(Exception exception) => exception switch
    {
        BadHttpRequestException badRequest => DescribeBadRequest(badRequest),
        _ => (Error.Failure(FailureCode), environment.IsDevelopment() ? exception.Message : null),
    };

    /// <summary>
    /// A malformed request is described in full even in production: it is about the payload the
    /// caller sent, so there is nothing of ours to leak.
    /// </summary>
    private static (Error Error, string? Detail) DescribeBadRequest(BadHttpRequestException exception)
    {
        if (exception.InnerException is JsonException { Path: { Length: > 0 } path } jsonException)
        {
            var field = path.TrimStart('$', '.');

            return (
                Error.Problem($"{InvalidFieldCodePrefix}{field}.invalid", field),
                $"Invalid value for field '{field}'. {jsonException.Message}");
        }

        return (Error.Problem(InvalidBodyCode), exception.Message);
    }
}
