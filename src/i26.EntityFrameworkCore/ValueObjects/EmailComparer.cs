using i26.Core.ValueObjects;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace i26.EntityFrameworkCore.ValueObjects;

/// <summary>Compares and hashes an <see cref="Email"/> by its value.</summary>
/// <remarks>
/// Without one, change tracking falls back to reference equality for a class, and assigning an
/// address equal to the one already there would be saved as a change. The snapshot is the instance
/// itself: an <c>Email</c> is immutable, so there is nothing to copy.
/// </remarks>
public sealed class EmailComparer : ValueComparer<Email>
{
    /// <summary>Creates the comparer.</summary>
    public EmailComparer()
        : base(
            (left, right) => left!.Value == right!.Value,
            email => email.Value.GetHashCode(StringComparison.Ordinal),
            email => email)
    {
    }
}
