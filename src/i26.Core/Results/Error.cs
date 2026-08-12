namespace i26.Core.Results;

/// <summary>A business failure, identified by a stable code.</summary>
/// <param name="Code">Stable identifier in <c>dot.camelCase</c>: <c>course.notFound</c>.</param>
/// <param name="Type">The kind of failure, which decides the HTTP status.</param>
/// <remarks>
/// Carries no text: an <see cref="IErrorTranslator"/> resolves it at the boundary. Equality is the
/// code and the type, so <c>result.Error == CourseErrors.NotFound</c> holds whatever the error
/// carries.
/// </remarks>
public record Error(string Code, ErrorType Type)
{
    /// <summary>The absence of an error, carried by every successful <see cref="Result"/>.</summary>
    public static readonly Error None = new(string.Empty, ErrorType.Failure);

    /// <summary>A null value showed up where one was not allowed.</summary>
    public static readonly Error NullValue = new("general.null", ErrorType.Failure);

    /// <summary>Values that fill the placeholders of the localized message, in order.</summary>
    public IReadOnlyList<object?>? Arguments { get; init; }

    /// <summary>Internal diagnostics. Never reaches the HTTP response.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

    /// <summary>HTTP status code matching <see cref="Type"/>.</summary>
    public int StatusCode => Type.ToStatusCode();

    /// <summary>Creates a <see cref="ErrorType.Failure"/> error, answered as 500.</summary>
    public static Error Failure(string code, params object?[] arguments) =>
        Create(code, ErrorType.Failure, arguments);

    /// <summary>Creates a <see cref="ErrorType.NotFound"/> error, answered as 404.</summary>
    public static Error NotFound(string code, params object?[] arguments) =>
        Create(code, ErrorType.NotFound, arguments);

    /// <summary>Creates a <see cref="ErrorType.Problem"/> error, answered as 400.</summary>
    public static Error Problem(string code, params object?[] arguments) =>
        Create(code, ErrorType.Problem, arguments);

    /// <summary>Creates a <see cref="ErrorType.Conflict"/> error, answered as 409.</summary>
    public static Error Conflict(string code, params object?[] arguments) =>
        Create(code, ErrorType.Conflict, arguments);

    /// <summary>Creates a <see cref="ErrorType.Validation"/> error, answered as 400.</summary>
    public static Error Validation(string code, params object?[] arguments) =>
        Create(code, ErrorType.Validation, arguments);

    /// <summary>Creates a <see cref="ErrorType.Forbidden"/> error, answered as 403.</summary>
    public static Error Forbidden(string code, params object?[] arguments) =>
        Create(code, ErrorType.Forbidden, arguments);

    /// <summary>Creates an <see cref="ErrorType.Unauthorized"/> error, answered as 401.</summary>
    public static Error Unauthorized(string code, params object?[] arguments) =>
        Create(code, ErrorType.Unauthorized, arguments);

    /// <summary>Creates a <see cref="ErrorType.PaymentRequired"/> error, answered as 402.</summary>
    public static Error PaymentRequired(string code, params object?[] arguments) =>
        Create(code, ErrorType.PaymentRequired, arguments);

    /// <summary>Creates a <see cref="ErrorType.Gone"/> error, answered as 410.</summary>
    public static Error Gone(string code, params object?[] arguments) =>
        Create(code, ErrorType.Gone, arguments);

    /// <summary>Creates a <see cref="ErrorType.UnprocessableContent"/> error, answered as 422.</summary>
    public static Error UnprocessableContent(string code, params object?[] arguments) =>
        Create(code, ErrorType.UnprocessableContent, arguments);

    /// <summary>Creates a <see cref="ErrorType.TooManyRequests"/> error, answered as 429.</summary>
    public static Error TooManyRequests(string code, params object?[] arguments) =>
        Create(code, ErrorType.TooManyRequests, arguments);

    /// <summary>Creates a <see cref="ErrorType.ServiceUnavailable"/> error, answered as 503.</summary>
    public static Error ServiceUnavailable(string code, params object?[] arguments) =>
        Create(code, ErrorType.ServiceUnavailable, arguments);

    /// <summary>Creates an error of any kind, for the ones without a factory of their own.</summary>
    public static Error Create(string code, ErrorType type, params object?[] arguments) =>
        new(code, type) { Arguments = arguments is { Length: > 0 } ? arguments : null };

    /// <summary>Returns a copy carrying the given message arguments.</summary>
    public Error WithArguments(params object?[] arguments) =>
        this with { Arguments = arguments is { Length: > 0 } ? arguments : null };

    /// <summary>Returns a copy carrying the given diagnostics.</summary>
    public Error WithMetadata(IReadOnlyDictionary<string, object?> metadata) =>
        this with { Metadata = metadata };

    /// <summary>Two errors are the same when they carry the same code and the same kind.</summary>
    /// <remarks>
    /// Not the member-by-member equality a record usually gives: arguments and metadata are payload,
    /// not identity.
    /// </remarks>
    public virtual bool Equals(Error? other) =>
        other is not null
        && EqualityContract == other.EqualityContract
        && string.Equals(Code, other.Code, StringComparison.Ordinal)
        && Type == other.Type;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(EqualityContract, Code, Type);
}
