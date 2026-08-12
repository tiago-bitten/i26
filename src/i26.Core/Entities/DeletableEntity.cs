using i26.Core.Ids;
using i26.Core.Results;

namespace i26.Core.Entities;

/// <summary>An entity that is deleted by saying so, and can say otherwise later.</summary>
/// <typeparam name="TId">The id type declared for this entity.</typeparam>
/// <remarks>
/// <see cref="Delete"/> is virtual: an entity that has a reason to refuse — an order already
/// shipped, a tenant with rows behind it — overrides it, answers with its own error, and calls this
/// one when the answer is yes.
/// </remarks>
public abstract class DeletableEntity<TId> : Entity<TId>, ISoftDeletable
    where TId : struct, ITypedId<TId>
{
    /// <inheritdoc cref="Entity{TId}()" />
    protected DeletableEntity()
    {
    }

    /// <inheritdoc cref="Entity{TId}(TId)" />
    protected DeletableEntity(TId id) : base(id)
    {
    }

    /// <inheritdoc />
    public bool IsDeleted { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Stamped where the other timestamps are — by the persistence layer, not by the entity, which
    /// has no clock. i26.EntityFrameworkCore does it in an interceptor.
    /// </remarks>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Marks the entity deleted.</summary>
    /// <returns><see cref="EntityErrors.AlreadyDeleted"/> if it already was.</returns>
    public virtual Result Delete()
    {
        if (IsDeleted)
        {
            return EntityErrors.AlreadyDeleted;
        }

        IsDeleted = true;

        return Result.Ok();
    }

    /// <summary>Brings the entity back.</summary>
    /// <returns><see cref="EntityErrors.NotDeleted"/> if it was never deleted.</returns>
    public virtual Result Restore()
    {
        if (!IsDeleted)
        {
            return EntityErrors.NotDeleted;
        }

        IsDeleted = false;
        DeletedAt = null;

        return Result.Ok();
    }
}
