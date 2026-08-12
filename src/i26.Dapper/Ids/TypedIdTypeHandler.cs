using System.Data;
using Dapper;
using i26.Core.Ids;

namespace i26.Dapper.Ids;

/// <summary>
/// Reads and writes a typed id as the string it is stored as.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
/// <remarks>
/// Dapper has no idea what a <see cref="ITypedId{TSelf}"/> is and falls back to a cast that cannot
/// work, so a query selecting an id column into a typed id property fails at materialization
/// without one of these. Registered by
/// <see cref="TypedIdDapperExtensions.AddTypedIdHandlers"/>.
/// </remarks>
public sealed class TypedIdTypeHandler<TId> : SqlMapper.TypeHandler<TId>
    where TId : struct, ITypedId<TId>
{
    /// <summary>Reads an id out of the value the database returned.</summary>
    /// <param name="value">The column value.</param>
    /// <returns>The id.</returns>
    /// <exception cref="FormatException">The text is not an id of this type.</exception>
    /// <exception cref="InvalidCastException">The column holds something an id cannot be read from.</exception>
    /// <remarks>
    /// The prefixed string is the canonical storage. A raw <see cref="Guid"/> is accepted too, for
    /// the column that predates the convention or belongs to someone else — the type is known from
    /// the property being filled, so nothing is guessed.
    /// </remarks>
    public override TId Parse(object value) => value switch
    {
        string text => TypedId.Parse<TId>(text),
        Guid guid => TId.FromGuid(guid),
        TId id => id,

        // DBNull, not null: a null column reaches a type handler as DBNull.Value, and the two look
        // nothing alike to a pattern.
        null or DBNull => throw new InvalidCastException(
            $"A null column cannot be read as {typeof(TId).Name}, which is a struct. Read it into a " +
            $"{typeof(TId).Name}? instead."),
        _ => throw new InvalidCastException(
            $"{typeof(TId).Name} is stored as text, and the column returned {value.GetType().Name}."),
    };

    /// <summary>Writes an id as the string it is stored as.</summary>
    /// <param name="parameter">The parameter to fill.</param>
    /// <param name="value">The id.</param>
    /// <exception cref="ArgumentNullException"><paramref name="parameter"/> is <see langword="null"/>.</exception>
    public override void SetValue(IDbDataParameter parameter, TId value)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        parameter.DbType = DbType.String;
        parameter.Value = TypedId.Format(value);
    }
}
