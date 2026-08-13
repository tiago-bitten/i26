using i26.Core.ValueObjects;

namespace i26.EntityFrameworkCore.ValueObjects;

/// <summary>Compares and hashes an <see cref="Email"/> by its value.</summary>
/// <remarks>The named form of <see cref="ValueObjectComparer{TValue}"/>, for a property that says so.</remarks>
public sealed class EmailComparer : ValueObjectComparer<Email>;
