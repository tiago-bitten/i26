namespace i26.Core.Results;

/// <summary>
/// A validation failure carrying every individual error at once, so the caller does not have to
/// fix one field per round trip.
/// </summary>
/// <param name="Errors">The individual errors.</param>
public sealed record ValidationError(Error[] Errors)
    : Error("validation.general", ErrorType.Validation)
{
    /// <summary>Collects the errors of every failed result into a single validation error.</summary>
    /// <param name="results">The results to inspect; successful ones are skipped.</param>
    /// <returns>A validation error holding the failures.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <see langword="null"/>.</exception>
    public static ValidationError FromResults(IEnumerable<Result> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        return new ValidationError(results
            .Where(result => result.IsFailure)
            .Select(result => result.Error)
            .ToArray());
    }

    /// <summary>
    /// Two validation errors are the same when they hold the same errors in the same order.
    /// </summary>
    /// <param name="other">The validation error to compare with.</param>
    /// <returns><see langword="true"/> when both hold the same failures.</returns>
    /// <remarks>
    /// The base compares by code alone, which every validation error shares, so the list is what
    /// tells them apart. Compared element by element rather than by reference, which is what the
    /// generated equality of a record would do with an array.
    /// </remarks>
    public bool Equals(ValidationError? other) =>
        other is not null
        && base.Equals(other)
        && Errors.AsSpan().SequenceEqual(other.Errors);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(base.GetHashCode());

        foreach (var error in Errors)
        {
            hash.Add(error);
        }

        return hash.ToHashCode();
    }
}
