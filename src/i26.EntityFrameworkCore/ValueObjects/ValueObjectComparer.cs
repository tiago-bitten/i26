using i26.Core.ValueObjects;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace i26.EntityFrameworkCore.ValueObjects;

/// <summary>Compares and hashes a value object by its value.</summary>
/// <typeparam name="TValue">The value object.</typeparam>
/// <remarks>
/// Without one, change tracking falls back to reference equality for a class, and assigning a value
/// equal to the one already there would be saved as a change. The snapshot is the instance itself:
/// a value object is immutable, so there is nothing to copy.
/// </remarks>
public class ValueObjectComparer<TValue> : ValueComparer<TValue>
    where TValue : class, IStringValueObject<TValue>
{
    /// <summary>Creates the comparer.</summary>
    public ValueObjectComparer()
        : base(
            (left, right) => left!.Value == right!.Value,
            value => value.Value.GetHashCode(StringComparison.Ordinal),
            value => value)
    {
    }
}
