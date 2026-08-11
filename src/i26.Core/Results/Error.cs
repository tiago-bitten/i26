namespace i26.Core.Results;

/// <summary>
/// A business failure, identified by a stable <paramref name="Code"/>.
/// </summary>
/// <param name="Code">
/// Stable identifier in <c>dot.camelCase</c> — root in <c>lowerCamelCase</c> naming the entity,
/// inner segments in <c>camelCase</c>: <c>course.notFound</c>, <c>classroom.teachingLevel.required</c>.
/// It is the contract the client keys off, so it must never change once shipped.
/// </param>
/// <param name="Type">The kind of failure, which decides the HTTP status.</param>
/// <remarks>
/// <para>
/// There is no human readable text here on purpose. The text depends on the caller's language,
/// which is a boundary concern: an <see cref="IErrorTranslator"/> resolves it from the code when the
/// response is written, and it never travels back into the domain. That also keeps the identity of
/// an error stable — see the note on equality below.
/// </para>
/// <para>
/// Codes belong in a static <c>{Entity}Errors</c> class next to the entity, never inline at the
/// call site. Errors whose message needs values carry them in <see cref="Arguments"/> and become
/// methods instead of fields:
/// </para>
/// <code>
/// public static class CourseErrors
/// {
///     public static readonly Error NotFound = Error.NotFound("course.notFound");
///     public static Error TitleTooLong(int max) => Error.Validation("course.title.tooLong", max);
/// }
/// </code>
/// <para>
/// <strong>Equality is the code and the type, nothing else.</strong> Two errors with the same code
/// are the same error whether or not they carry arguments or metadata, so
/// <c>result.Error == CourseErrors.TitleTooLong(200)</c> and a <c>switch</c> over error codes behave
/// the way you would expect. This deviates from the member-by-member equality a record usually
/// gives you, and it is deliberate: arguments and metadata are payload, not identity.
/// </para>
/// </remarks>
public record Error(string Code, ErrorType Type)
{
    /// <summary>The absence of an error. Carried by every successful <see cref="Result"/>.</summary>
    public static readonly Error None = new(string.Empty, ErrorType.Failure);

    /// <summary>A null value showed up where one was not allowed.</summary>
    public static readonly Error NullValue = new("general.null", ErrorType.Failure);

    /// <summary>
    /// Values that fill the placeholders of the localized message, in order;
    /// <see langword="null"/> when the message has none.
    /// </summary>
    /// <remarks>
    /// They belong to the message, not to the diagnostics: an <see cref="IErrorTranslator"/> formats
    /// the template with them, so <c>course.title.tooLong</c> can render "Title is longer than 200
    /// characters" in any language without the domain ever composing a sentence.
    /// </remarks>
    public IReadOnlyList<object?>? Arguments { get; init; }

    /// <summary>
    /// Optional bag for internal diagnostics that has to cross an adapter boundary without a
    /// contract of its own; <see langword="null"/> when there is none.
    /// </summary>
    /// <remarks>
    /// It is not part of the HTTP response — the transport layer builds the payload field by field
    /// and never reflects over this.
    /// </remarks>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

    /// <summary>HTTP status code matching <see cref="Type"/>.</summary>
    /// <remarks>
    /// The mapping itself lives in <see cref="ErrorTypeExtensions.ToStatusCode"/>, so it can also
    /// be asked about a kind of failure with no error at hand.
    /// </remarks>
    public int StatusCode => Type.ToStatusCode();

    /// <summary>Creates an <see cref="ErrorType.Failure"/> error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="arguments">Values for the placeholders of the localized message, if any.</param>
    /// <returns>The error.</returns>
    public static Error Failure(string code, params object?[] arguments) =>
        Create(code, ErrorType.Failure, arguments);

    /// <summary>Creates an <see cref="ErrorType.NotFound"/> error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="arguments">Values for the placeholders of the localized message, if any.</param>
    /// <returns>The error.</returns>
    public static Error NotFound(string code, params object?[] arguments) =>
        Create(code, ErrorType.NotFound, arguments);

    /// <summary>Creates an <see cref="ErrorType.Problem"/> error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="arguments">Values for the placeholders of the localized message, if any.</param>
    /// <returns>The error.</returns>
    public static Error Problem(string code, params object?[] arguments) =>
        Create(code, ErrorType.Problem, arguments);

    /// <summary>Creates an <see cref="ErrorType.Conflict"/> error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="arguments">Values for the placeholders of the localized message, if any.</param>
    /// <returns>The error.</returns>
    public static Error Conflict(string code, params object?[] arguments) =>
        Create(code, ErrorType.Conflict, arguments);

    /// <summary>Creates an <see cref="ErrorType.Validation"/> error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="arguments">Values for the placeholders of the localized message, if any.</param>
    /// <returns>The error.</returns>
    public static Error Validation(string code, params object?[] arguments) =>
        Create(code, ErrorType.Validation, arguments);

    /// <summary>Creates an <see cref="ErrorType.Forbidden"/> error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="arguments">Values for the placeholders of the localized message, if any.</param>
    /// <returns>The error.</returns>
    public static Error Forbidden(string code, params object?[] arguments) =>
        Create(code, ErrorType.Forbidden, arguments);

    /// <summary>Creates an <see cref="ErrorType.Unauthorized"/> error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="arguments">Values for the placeholders of the localized message, if any.</param>
    /// <returns>The error.</returns>
    public static Error Unauthorized(string code, params object?[] arguments) =>
        Create(code, ErrorType.Unauthorized, arguments);

    /// <summary>Creates an <see cref="ErrorType.PaymentRequired"/> error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="arguments">Values for the placeholders of the localized message, if any.</param>
    /// <returns>The error.</returns>
    public static Error PaymentRequired(string code, params object?[] arguments) =>
        Create(code, ErrorType.PaymentRequired, arguments);

    /// <summary>Creates an <see cref="ErrorType.Gone"/> error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="arguments">Values for the placeholders of the localized message, if any.</param>
    /// <returns>The error.</returns>
    public static Error Gone(string code, params object?[] arguments) =>
        Create(code, ErrorType.Gone, arguments);

    /// <summary>Creates an <see cref="ErrorType.UnprocessableContent"/> error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="arguments">Values for the placeholders of the localized message, if any.</param>
    /// <returns>The error.</returns>
    public static Error UnprocessableContent(string code, params object?[] arguments) =>
        Create(code, ErrorType.UnprocessableContent, arguments);

    /// <summary>Creates an <see cref="ErrorType.TooManyRequests"/> error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="arguments">Values for the placeholders of the localized message, if any.</param>
    /// <returns>The error.</returns>
    public static Error TooManyRequests(string code, params object?[] arguments) =>
        Create(code, ErrorType.TooManyRequests, arguments);

    /// <summary>Creates an <see cref="ErrorType.ServiceUnavailable"/> error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="arguments">Values for the placeholders of the localized message, if any.</param>
    /// <returns>The error.</returns>
    public static Error ServiceUnavailable(string code, params object?[] arguments) =>
        Create(code, ErrorType.ServiceUnavailable, arguments);

    /// <summary>Creates an error of any type, for the ones without a factory of their own.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="type">The kind of failure.</param>
    /// <param name="arguments">Values for the placeholders of the localized message, if any.</param>
    /// <returns>The error.</returns>
    public static Error Create(string code, ErrorType type, params object?[] arguments) =>
        new(code, type) { Arguments = arguments is { Length: > 0 } ? arguments : null };

    /// <summary>Returns a copy of this error carrying the given message arguments.</summary>
    /// <param name="arguments">Values for the placeholders of the localized message.</param>
    /// <returns>A new error; this instance is left untouched.</returns>
    public Error WithArguments(params object?[] arguments) =>
        this with { Arguments = arguments is { Length: > 0 } ? arguments : null };

    /// <summary>Returns a copy of this error carrying the given metadata.</summary>
    /// <param name="metadata">The diagnostic bag to attach.</param>
    /// <returns>A new error; this instance is left untouched.</returns>
    public Error WithMetadata(IReadOnlyDictionary<string, object?> metadata) =>
        this with { Metadata = metadata };

    /// <summary>Two errors are the same when they carry the same code and the same type.</summary>
    /// <param name="other">The error to compare with.</param>
    /// <returns><see langword="true"/> when both describe the same failure.</returns>
    public virtual bool Equals(Error? other) =>
        other is not null
        && EqualityContract == other.EqualityContract
        && string.Equals(Code, other.Code, StringComparison.Ordinal)
        && Type == other.Type;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(EqualityContract, Code, Type);
}
