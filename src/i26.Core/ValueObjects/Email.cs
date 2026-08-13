using i26.Core.Results;

namespace i26.Core.ValueObjects;

/// <summary>An email address that was checked once, at the edge, and is trusted after that.</summary>
/// <remarks>
/// Created through <see cref="Create"/>, which answers with a <see cref="Result{T}"/> rather than
/// throwing, so a bad address travels the same way every other refusal does. There is no public
/// constructor: an <c>Email</c> in a signature is an address that already passed.
/// </remarks>
public sealed record Email
{
    /// <summary>The longest an address may be, in characters.</summary>
    /// <remarks>RFC 5321: 254 is the ceiling a path can carry, and it is also a sane column width.</remarks>
    public const int MaxLength = 254;

    /// <summary>The longest the part before the <c>@</c> may be.</summary>
    public const int MaxLocalPartLength = 64;

    private const int MaxDomainLabelLength = 63;

    private Email(string value, int at)
    {
        Value = value;
        LocalPart = value[..at];
        Domain = value[(at + 1)..];
    }

    /// <summary>The address, trimmed and lowercased.</summary>
    public string Value { get; }

    /// <summary>What comes before the <c>@</c>.</summary>
    public string LocalPart { get; }

    /// <summary>What comes after the <c>@</c>.</summary>
    public string Domain { get; }

    /// <summary>Checks an address and answers with it, or with why not.</summary>
    /// <remarks>
    /// Trimmed and lowercased on the way in, so two people typing the same address the same way get
    /// the same value — which is what makes equality and a unique index agree with each other.
    /// </remarks>
    public static Result<Email> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EmailErrors.Required;
        }

        var address = value.Trim().ToLowerInvariant();

        if (address.Length > MaxLength)
        {
            return EmailErrors.TooLong(MaxLength);
        }

        var at = address.IndexOf('@', StringComparison.Ordinal);

        if (at <= 0 || at == address.Length - 1 || address.IndexOf('@', at + 1) >= 0)
        {
            return EmailErrors.Malformed;
        }

        if (!IsLocalPart(address.AsSpan(0, at)))
        {
            return EmailErrors.InvalidLocalPart;
        }

        if (!IsDomain(address.AsSpan(at + 1)))
        {
            return EmailErrors.InvalidDomain;
        }

        return new Email(address, at);
    }

    /// <summary>Reads an address that is already known to be one.</summary>
    /// <param name="value">The stored address.</param>
    /// <returns>The address.</returns>
    /// <exception cref="FormatException">It is not one after all.</exception>
    /// <remarks>
    /// For data this application wrote itself — a row, a fixture. Anything arriving from outside
    /// goes through <see cref="Create"/>, which refuses without throwing.
    /// </remarks>
    public static Email Parse(string? value)
    {
        var result = Create(value);

        return result.IsSuccess
            ? result.Value
            : throw new FormatException($"'{value}' is not an email address ({result.Error.Code}).");
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    // Written out rather than matched with a regular expression: the rule is a set of character
    // classes, and a loop says which one failed without a pattern nobody can read.
    private static bool IsLocalPart(ReadOnlySpan<char> local)
    {
        if (local.Length > MaxLocalPartLength || local[0] is '.' || local[^1] is '.')
        {
            return false;
        }

        for (var index = 0; index < local.Length; index++)
        {
            var character = local[index];

            if (character is '.' && local[index - 1] is '.')
            {
                return false;
            }

            if (!IsLocalPartCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    // The unquoted local part of RFC 5322, minus the punctuation nobody uses and every provider
    // rejects. A quoted one — "john doe"@example.com — is legal and refused here on purpose.
    private static bool IsLocalPartCharacter(char character) =>
        character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-' or '+';

    private static bool IsDomain(ReadOnlySpan<char> domain)
    {
        if (domain[0] is '.' or '-' || domain[^1] is '.' or '-')
        {
            return false;
        }

        var labels = 0;
        var length = 0;

        foreach (var character in domain)
        {
            if (character is '.')
            {
                if (length is 0)
                {
                    return false;
                }

                labels++;
                length = 0;
                continue;
            }

            if (!IsDomainCharacter(character) || ++length > MaxDomainLabelLength)
            {
                return false;
            }
        }

        // A dot and something after it: a domain with no dot is a host name on a local network, and
        // an address is not being checked here to be delivered on one.
        return labels > 0;
    }

    private static bool IsDomainCharacter(char character) =>
        character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';
}
