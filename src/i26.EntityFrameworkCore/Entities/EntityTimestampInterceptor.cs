using i26.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace i26.EntityFrameworkCore.Entities;

/// <summary>Stamps <c>CreatedAt</c>, <c>UpdatedAt</c> and <c>DeletedAt</c> as rows are saved.</summary>
/// <remarks>
/// The entity has no clock and is not given one: it says that it was created, changed or deleted,
/// and this says when. Reads the time from a <see cref="TimeProvider"/>, so a test can decide what
/// "now" is.
/// </remarks>
public sealed class EntityTimestampInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _time;

    /// <summary>Creates an interceptor reading the time from <paramref name="time"/>.</summary>
    /// <param name="time">Defaults to <see cref="TimeProvider.System"/>.</param>
    public EntityTimestampInterceptor(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Stamp(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Stamp(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _time.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<IEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    Set(entry, nameof(IEntity.CreatedAt), now);
                    Set(entry, nameof(IEntity.UpdatedAt), now);
                    break;

                case EntityState.Modified:
                    Set(entry, nameof(IEntity.UpdatedAt), now);
                    StampDeletion(entry, now);
                    break;

                default:
                    break;
            }
        }
    }

    // A soft delete is a modification like any other, so the instant has to come from the property
    // that changed rather than from the state: IsDeleted going true is the delete.
    private static void StampDeletion(EntityEntry entry, DateTimeOffset now)
    {
        if (entry.Entity is not ISoftDeletable deletable)
        {
            return;
        }

        var isDeleted = entry.Property(nameof(ISoftDeletable.IsDeleted));

        if (isDeleted.IsModified && deletable.IsDeleted)
        {
            Set(entry, nameof(ISoftDeletable.DeletedAt), now);
        }
    }

    // Through the property rather than the setter: the entity keeps them private, which is what
    // stops application code from deciding when something was created.
    private static void Set(EntityEntry entry, string property, DateTimeOffset value) =>
        entry.Property(property).CurrentValue = value;
}
