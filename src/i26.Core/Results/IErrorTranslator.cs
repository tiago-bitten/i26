namespace i26.Core.Results;

/// <summary>Resolves the text of an error, usually from resources keyed by <see cref="Error.Code"/>.</summary>
/// <remarks>
/// This is what lets <see cref="Error"/> hold no text. An implementation formats its template with
/// <see cref="Error.Arguments"/>, and must be safe to call concurrently.
/// </remarks>
public interface IErrorTranslator
{
    /// <summary>Returns the text describing an error, or null when there is none for its code.</summary>
    /// <remarks>Null leaves the field out of the response rather than sending an empty string.</remarks>
    string? Describe(Error error);
}
