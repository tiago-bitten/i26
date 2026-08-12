using System.Linq.Expressions;

namespace i26.Core.Specifications;

/// <summary>A specification written as one expression.</summary>
/// <typeparam name="T">What the rule is about.</typeparam>
public abstract class Specification<T> : ISpecification<T>
{
    private Func<T, bool>? _predicate;

    /// <inheritdoc />
    public abstract Expression<Func<T, bool>> ToExpression();

    /// <inheritdoc />
    /// <remarks>Compiled once per instance and kept.</remarks>
    // Compiling costs a thousand times what the call does, and asking one specification of every
    // item of a list is how this gets called.
    public bool IsSatisfiedBy(T candidate) => (_predicate ??= ToExpression().Compile())(candidate);
}

internal sealed class AndSpecification<T>(ISpecification<T> left, ISpecification<T> right) : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression() =>
        Predicates.Combine(left.ToExpression(), right.ToExpression(), Expression.AndAlso);
}

internal sealed class OrSpecification<T>(ISpecification<T> left, ISpecification<T> right) : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression() =>
        Predicates.Combine(left.ToExpression(), right.ToExpression(), Expression.OrElse);
}

internal sealed class NotSpecification<T>(ISpecification<T> specification) : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression() =>
        Predicates.Negate(specification.ToExpression());
}
