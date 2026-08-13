using i26.Core.Results;

namespace i26.Core.ValueObjects;

/// <summary>Why an address was refused.</summary>
/// <remarks>
/// One code per rule rather than one for "invalid": a form telling someone their address is too
/// long is worth more than one telling them it is wrong.
/// </remarks>
public static class EmailErrors
{
    /// <summary>Nothing was given.</summary>
    public static readonly Error Required = Error.Validation("email.required");

    /// <summary>Longer than an address is allowed to be.</summary>
    public static Error TooLong(int max) => Error.Validation("email.tooLong", max);

    /// <summary>No <c>@</c>, more than one, or nothing on one side of it.</summary>
    public static readonly Error Malformed = Error.Validation("email.malformed");

    /// <summary>The part before the <c>@</c> is empty, too long, or has a character it cannot.</summary>
    public static readonly Error InvalidLocalPart = Error.Validation("email.localPart.invalid");

    /// <summary>The part after the <c>@</c> is not a domain name.</summary>
    public static readonly Error InvalidDomain = Error.Validation("email.domain.invalid");
}
