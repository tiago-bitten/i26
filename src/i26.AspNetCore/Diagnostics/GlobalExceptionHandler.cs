using System.Text.Json;
using i26.AspNetCore.Results;
using i26.Core.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace i26.AspNetCore.Diagnostics;

/// <summary>
/// Turns anything that escaped a handler into the same problem response a failed
/// <see cref="Result"/> would have produced, so a client parses a crash like a business failure.
/// </summary>
/// <remarks>
/// Register with <c>AddProblemDetails</c>, <c>AddExceptionHandler&lt;GlobalExceptionHandler&gt;</c>
/// and <c>UseExceptionHandler</c>. A <see cref="BadHttpRequestException"/> comes back as 400 naming
/// the field, so endpoints never check for it; anything else is a bug and comes back as 500.
/// </remarks>
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
    /// <returns><see langword="true"/> when the response was written.</returns>
    /// <remarks>
    /// The exception message reaches the client only in Development: messages routinely spell out
    /// connection strings, file paths and SQL. The full exception is always in the log.
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
