namespace i26.Core.Entities;

/// <summary>An entity that is deleted by saying so rather than by disappearing.</summary>
/// <remarks>
/// The marker a query filter reaches for: <c>modelBuilder.ApplySoftDeleteFilter()</c> in
/// i26.EntityFrameworkCore hides these rows from every query that does not ask for them.
/// </remarks>
public interface ISoftDeletable
{
    /// <summary>Whether the row is deleted.</summary>
    bool IsDeleted { get; }

    /// <summary>When it was deleted, or <see langword="null"/> while it is not.</summary>
    DateTimeOffset? DeletedAt { get; }
}
