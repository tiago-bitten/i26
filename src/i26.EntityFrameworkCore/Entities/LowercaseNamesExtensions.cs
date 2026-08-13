using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.Entities;

/// <summary>Naming convention for the database side of the model.</summary>
public static class LowercaseNamesExtensions
{
    /// <summary>Lowercases every name the model puts in the database.</summary>
    /// <param name="modelBuilder">The model being built.</param>
    /// <returns>The same <paramref name="modelBuilder"/>, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Tables, columns, keys, foreign keys and indexes: <c>UserAuth</c> becomes <c>userauth</c> and
    /// <c>CreatedAt</c> becomes <c>createdat</c>. On Postgres an identifier that is not lowercase
    /// has to be quoted everywhere, forever — in every migration, every hand-written query and
    /// every psql session — because an unquoted one is folded to lowercase and no longer matches.
    /// </para>
    /// <para>
    /// Call it **last** in <c>OnModelCreating</c>: it rewrites the names the configurations before
    /// it decided, including the ones they set by hand.
    /// </para>
    /// </remarks>
    public static ModelBuilder ApplyLowercaseNames(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            entityType.SetTableName(Lower(entityType.GetTableName()));
            entityType.SetSchema(Lower(entityType.GetSchema()));

            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(Lower(property.GetColumnName()));
            }

            foreach (var key in entityType.GetKeys())
            {
                key.SetName(Lower(key.GetName()));
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.SetConstraintName(Lower(foreignKey.GetConstraintName()));
            }

            foreach (var index in entityType.GetIndexes())
            {
                index.SetDatabaseName(Lower(index.GetDatabaseName()));
            }
        }

        return modelBuilder;
    }

    // Invariant, not the current culture: a Turkish machine would otherwise name the column
    // 'ıd' and the migration would not match the database anybody else built.
    private static string? Lower(string? name) => name?.ToLowerInvariant();
}
