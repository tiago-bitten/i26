using i26.Core.Ids;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace i26.EntityFrameworkCore.Ids;

/// <summary>
/// Compares and hashes a typed id by its <see cref="ITypedId{TSelf}.Value"/>, so EF Core's change
/// tracking does not rely on the struct's default equality.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
/// <remarks>
/// The snapshot is the value itself: a typed id is immutable, so there is nothing to copy.
/// </remarks>
public sealed class TypedIdComparer<TId> : ValueComparer<TId>
    where TId : struct, ITypedId<TId>
{
    /// <summary>Creates the comparer.</summary>
    public TypedIdComparer()
        : base(
            (left, right) => left.Value == right.Value,
            id => id.Value.GetHashCode(),
            id => id)
    {
    }
}
