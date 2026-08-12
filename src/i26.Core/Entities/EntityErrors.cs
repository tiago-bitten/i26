using i26.Core.Results;

namespace i26.Core.Entities;

/// <summary>What a base entity refuses to do.</summary>
public static class EntityErrors
{
    /// <summary>Deleting something that is already deleted.</summary>
    public static readonly Error AlreadyDeleted = Error.Conflict("entity.alreadyDeleted");

    /// <summary>Restoring something that was never deleted.</summary>
    public static readonly Error NotDeleted = Error.Conflict("entity.notDeleted");
}
