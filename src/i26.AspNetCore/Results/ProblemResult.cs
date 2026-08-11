using i26.Core.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace i26.AspNetCore.Results;

/// <summary>
/// The <see cref="IResult"/> produced by <see cref="ProblemResults"/>. Resolving the translator is
/// deferred to execution time, where the request's services are available.
/// </summary>
/// <param name="error">The failure to describe.</param>
/// <param name="fallbackDetail">
/// Text for the <c>detail</c> member when no translator has anything to say about the code. Used by
/// the exception handler, which has a message of its own to offer; left null everywhere else.
/// </param>
internal sealed class ProblemResult(Error error, string? fallbackDetail = null) : IResult
{
    private const string CodeExtension = "code";
    private const string ErrorsExtension = "errors";

    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var translator = httpContext.RequestServices?.GetService<IErrorTranslator>();

        return Microsoft.AspNetCore.Http.Results.Problem(
                title: error.Code,
                detail: Describe(error, translator) ?? Trimmed(fallbackDetail),
                type: ProblemResults.GetTypeUri(error.StatusCode),
                statusCode: error.StatusCode,
                extensions: BuildExtensions(error, translator))
            .ExecuteAsync(httpContext);
    }

    /// <summary>
    /// Asks the translator for the text of an error. Nothing to say means the member is left out —
    /// RFC 9457 makes it optional, and an empty string would be noise on the wire.
    /// </summary>
    private static string? Describe(Error error, IErrorTranslator? translator) =>
        Trimmed(translator?.Describe(error));

    private static string? Trimmed(string? value) => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>
    /// Builds the problem details extensions field by field. <see cref="Error.Metadata"/> is left
    /// out on purpose: it is internal diagnostics and must not reach the client.
    /// </summary>
    private static Dictionary<string, object?> BuildExtensions(Error error, IErrorTranslator? translator)
    {
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [CodeExtension] = error.Code,
        };

        if (error is ValidationError validationError)
        {
            // Each one is described on its own; the outer error only says "something did not validate".
            extensions[ErrorsExtension] = Array.ConvertAll(
                validationError.Errors,
                item => new ProblemError(item.Code, Describe(item, translator)));
        }

        return extensions;
    }

    /// <summary>One entry of the <c>errors</c> extension of a validation problem.</summary>
    /// <param name="Code">The error code.</param>
    /// <param name="Message">The description, when the translator had one.</param>
    private sealed record ProblemError(string Code, string? Message);
}
