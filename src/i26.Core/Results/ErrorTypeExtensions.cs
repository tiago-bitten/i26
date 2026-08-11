namespace i26.Core.Results;

/// <summary>
/// The single mapping from a kind of failure to an HTTP status code.
/// </summary>
/// <remarks>
/// <see cref="Error.StatusCode"/> delegates here, so the transport layer can ask what status a
/// kind of error will produce without having an error at hand — declaring an endpoint's possible
/// responses, for instance.
/// </remarks>
public static class ErrorTypeExtensions
{
    /// <summary>The HTTP status code an error of this kind is answered with.</summary>
    /// <param name="type">The kind of failure.</param>
    /// <returns>The status code.</returns>
    /// <remarks>
    /// Every declared <see cref="ErrorType"/> has an arm here. The fallback only catches a value
    /// cast in from outside the enum, and answers 500 because an unrecognized failure is a server
    /// problem, not the caller's.
    /// </remarks>
    public static int ToStatusCode(this ErrorType type) => type switch
    {
        // Client errors (4xx).
        ErrorType.Validation => 400,
        ErrorType.Problem => 400,
        ErrorType.Unauthorized => 401,
        ErrorType.PaymentRequired => 402,
        ErrorType.Forbidden => 403,
        ErrorType.NotFound => 404,
        ErrorType.MethodNotAllowed => 405,
        ErrorType.NotAcceptable => 406,
        ErrorType.ProxyAuthenticationRequired => 407,
        ErrorType.RequestTimeout => 408,
        ErrorType.Conflict => 409,
        ErrorType.Gone => 410,
        ErrorType.LengthRequired => 411,
        ErrorType.PreconditionFailed => 412,
        ErrorType.ContentTooLarge => 413,
        ErrorType.UriTooLong => 414,
        ErrorType.UnsupportedMediaType => 415,
        ErrorType.RangeNotSatisfiable => 416,
        ErrorType.ExpectationFailed => 417,
        ErrorType.MisdirectedRequest => 421,
        ErrorType.UnprocessableContent => 422,
        ErrorType.Locked => 423,
        ErrorType.FailedDependency => 424,
        ErrorType.TooEarly => 425,
        ErrorType.UpgradeRequired => 426,
        ErrorType.PreconditionRequired => 428,
        ErrorType.TooManyRequests => 429,
        ErrorType.RequestHeaderFieldsTooLarge => 431,
        ErrorType.UnavailableForLegalReasons => 451,

        // Server errors (5xx).
        ErrorType.Failure => 500,
        ErrorType.NotImplemented => 501,
        ErrorType.BadGateway => 502,
        ErrorType.ServiceUnavailable => 503,
        ErrorType.GatewayTimeout => 504,
        ErrorType.HttpVersionNotSupported => 505,
        ErrorType.VariantAlsoNegotiates => 506,
        ErrorType.InsufficientStorage => 507,
        ErrorType.LoopDetected => 508,
        ErrorType.NotExtended => 510,
        ErrorType.NetworkAuthenticationRequired => 511,

        _ => 500,
    };
}
