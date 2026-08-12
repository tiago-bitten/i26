using System.Linq.Expressions;

namespace i26.Core.Specifications;

/// <summary>A rule about one thing, written once and asked of a row or of a database.</summary>
/// <typeparam name="T">What the rule is about.</typeparam>
/// <remarks>The two members cannot drift: the rule that filters the query is the one that answers here.</remarks>
public interface ISpecification<T>
{
    /// <summary>The rule, as an expression a query provider can translate.</summary>
    Expression<Func<T, bool>> ToExpression();

    /// <summary>Whether the candidate satisfies the rule.</summary>
    bool IsSatisfiedBy(T candidate);
}
