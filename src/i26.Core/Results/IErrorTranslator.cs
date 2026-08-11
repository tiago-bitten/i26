namespace i26.Core.Results;

/// <summary>
/// Resolves the human readable text of an error, usually from localized resources keyed by
/// <see cref="Error.Code"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is what lets <see cref="Error"/> hold no text at all: the domain names the failure, the
/// boundary says it out loud, in the language the caller asked for. The text is returned, never
/// written back into the error.
/// </para>
/// <para>
/// An implementation looks the code up and formats the template with
/// <see cref="Error.Arguments"/>:
/// </para>
/// <code>
/// public string? Describe(Error error)
/// {
///     var template = localizer[error.Code];
///
///     if (template == error.Code)
///     {
///         return null;   // no entry for this code
///     }
///
///     return error.Arguments is { Count: > 0 } arguments
///         ? string.Format(CultureInfo.CurrentCulture, template, [.. arguments])
///         : template;
/// }
/// </code>
/// <para>Implementations must be safe to call concurrently.</para>
/// </remarks>
public interface IErrorTranslator
{
    /// <summary>Returns the text describing an error.</summary>
    /// <param name="error">The error to describe.</param>
    /// <returns>
    /// The description, or <see langword="null"/> when there is no text for this code — in which
    /// case the transport layer leaves the field out rather than sending an empty string.
    /// </returns>
    string? Describe(Error error);
}
