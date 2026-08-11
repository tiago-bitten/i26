namespace i26.Core.Results;

/// <summary>
/// Kind of failure an <see cref="Error"/> represents. It is what decides the HTTP status code at
/// the transport boundary, so the application layer never has to know about HTTP.
/// </summary>
/// <remarks>
/// <para>
/// The first six members are the ones business rules reach for day to day. The rest exist so an
/// adapter can relay an exact status — a gateway propagating an upstream <c>429</c> or <c>502</c>,
/// a rate limiter, a payment wall — without inventing a mapping of its own.
/// </para>
/// <para>
/// The numeric values are explicit and stable: they are part of the contract for anything that
/// persists or serializes an error type. Append new members, never renumber the existing ones.
/// The values are ordinals, not status codes, because <see cref="Validation"/> and
/// <see cref="Problem"/> are distinct kinds that share status 400.
/// </para>
/// </remarks>
public enum ErrorType
{
    /// <summary>Something broke that the caller cannot fix. Maps to 500 Internal Server Error.</summary>
    Failure = 0,

    /// <summary>The input did not pass validation. Maps to 400 Bad Request.</summary>
    Validation = 1,

    /// <summary>The request is well formed but the operation is not allowed right now. Maps to 400 Bad Request.</summary>
    Problem = 2,

    /// <summary>The resource does not exist. Maps to 404 Not Found.</summary>
    NotFound = 3,

    /// <summary>The state of the resource conflicts with the operation. Maps to 409 Conflict.</summary>
    Conflict = 4,

    /// <summary>The caller is known but not allowed. Maps to 403 Forbidden.</summary>
    Forbidden = 5,

    // Client errors (4xx).

    /// <summary>The caller did not authenticate, or the credentials expired. Maps to 401 Unauthorized.</summary>
    Unauthorized = 6,

    /// <summary>The operation requires a payment that has not been made. Maps to 402 Payment Required.</summary>
    PaymentRequired = 7,

    /// <summary>The resource does not support this method. Maps to 405 Method Not Allowed.</summary>
    MethodNotAllowed = 8,

    /// <summary>No representation matches what the caller accepts. Maps to 406 Not Acceptable.</summary>
    NotAcceptable = 9,

    /// <summary>The proxy between caller and server needs authentication. Maps to 407 Proxy Authentication Required.</summary>
    ProxyAuthenticationRequired = 10,

    /// <summary>The caller took too long to send the request. Maps to 408 Request Timeout.</summary>
    RequestTimeout = 11,

    /// <summary>The resource existed and is permanently gone. Maps to 410 Gone.</summary>
    Gone = 12,

    /// <summary>The request needs a declared content length. Maps to 411 Length Required.</summary>
    LengthRequired = 13,

    /// <summary>A conditional header did not hold — typically a lost update. Maps to 412 Precondition Failed.</summary>
    PreconditionFailed = 14,

    /// <summary>The payload is larger than the server accepts. Maps to 413 Content Too Large.</summary>
    ContentTooLarge = 15,

    /// <summary>The request target is longer than the server accepts. Maps to 414 URI Too Long.</summary>
    UriTooLong = 16,

    /// <summary>The payload is in a format the resource does not accept. Maps to 415 Unsupported Media Type.</summary>
    UnsupportedMediaType = 17,

    /// <summary>The requested range does not exist in the representation. Maps to 416 Range Not Satisfiable.</summary>
    RangeNotSatisfiable = 18,

    /// <summary>The <c>Expect</c> header cannot be satisfied. Maps to 417 Expectation Failed.</summary>
    ExpectationFailed = 19,

    /// <summary>The request reached a server that cannot produce a response for it. Maps to 421 Misdirected Request.</summary>
    MisdirectedRequest = 20,

    /// <summary>
    /// The payload parses but is semantically wrong. Maps to 422 Unprocessable Content — use it when
    /// <see cref="Validation"/>'s 400 is too blunt and the client needs to know the syntax was fine.
    /// </summary>
    UnprocessableContent = 21,

    /// <summary>The resource is locked by someone else. Maps to 423 Locked.</summary>
    Locked = 22,

    /// <summary>The operation depended on another one that failed. Maps to 424 Failed Dependency.</summary>
    FailedDependency = 23,

    /// <summary>The server will not risk replaying an early-data request. Maps to 425 Too Early.</summary>
    TooEarly = 24,

    /// <summary>The caller must switch protocols to continue. Maps to 426 Upgrade Required.</summary>
    UpgradeRequired = 25,

    /// <summary>The request must be conditional to avoid a lost update. Maps to 428 Precondition Required.</summary>
    PreconditionRequired = 26,

    /// <summary>The caller went over its rate limit or quota. Maps to 429 Too Many Requests.</summary>
    TooManyRequests = 27,

    /// <summary>The request headers are larger than the server accepts. Maps to 431 Request Header Fields Too Large.</summary>
    RequestHeaderFieldsTooLarge = 28,

    /// <summary>The resource is withheld for legal reasons. Maps to 451 Unavailable For Legal Reasons.</summary>
    UnavailableForLegalReasons = 29,

    // Server errors (5xx).

    /// <summary>The operation is not implemented yet. Maps to 501 Not Implemented.</summary>
    NotImplemented = 30,

    /// <summary>An upstream dependency answered with garbage. Maps to 502 Bad Gateway.</summary>
    BadGateway = 31,

    /// <summary>The service is down or overloaded and the caller should retry. Maps to 503 Service Unavailable.</summary>
    ServiceUnavailable = 32,

    /// <summary>An upstream dependency took too long. Maps to 504 Gateway Timeout.</summary>
    GatewayTimeout = 33,

    /// <summary>The HTTP version of the request is not supported. Maps to 505 HTTP Version Not Supported.</summary>
    HttpVersionNotSupported = 34,

    /// <summary>Content negotiation ended in a circular reference. Maps to 506 Variant Also Negotiates.</summary>
    VariantAlsoNegotiates = 35,

    /// <summary>There is no room left to store the representation. Maps to 507 Insufficient Storage.</summary>
    InsufficientStorage = 36,

    /// <summary>The operation walked into an infinite loop. Maps to 508 Loop Detected.</summary>
    LoopDetected = 37,

    /// <summary>The request needs an extension the server does not have. Maps to 510 Not Extended.</summary>
    NotExtended = 38,

    /// <summary>The caller must authenticate with the network first. Maps to 511 Network Authentication Required.</summary>
    NetworkAuthenticationRequired = 39,
}
