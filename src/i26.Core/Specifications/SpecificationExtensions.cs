namespace i26.Core.Specifications;

/// <summary>Building a specification out of the ones already written.</summary>
/// <remarks>
/// Extensions rather than members, so a rule that only implements <see cref="ISpecification{T}"/>
/// composes the same way.
/// </remarks>
public static class SpecificationExtensions
{
    /// <summary>Both rules.</summary>
    public static Specification<T> And<T>(this ISpecification<T> left, ISpecification<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new AndSpecification<T>(left, right);
    }

    /// <summary>Either rule.</summary>
    public static Specification<T> Or<T>(this ISpecification<T> left, ISpecification<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new OrSpecification<T>(left, right);
    }

    /// <summary>The rule, inverted.</summary>
    public static Specification<T> Not<T>(this ISpecification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);

        return new NotSpecification<T>(specification);
    }
}
