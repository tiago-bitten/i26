using System.Linq.Expressions;

namespace i26.Core.Specifications;

/// <summary>Joins two predicates into one a query provider can still read.</summary>
internal static class Predicates
{
    /// <summary>Two predicates joined over a single parameter.</summary>
    // The shorter way is AndAlso(Invoke(left, p), Invoke(right, p)), and Entity Framework does
    // translate it — its pipeline removes invocations first. Rebinding the second lambda onto the
    // first one's parameter asks nobody for that favour: what comes out is indistinguishable from
    // x => a && b written by hand, which is what the next backend will need it to be.
    internal static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> join)
    {
        var parameter = left.Parameters[0];
        var rebound = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);

        return Expression.Lambda<Func<T, bool>>(join(left.Body, rebound), parameter);
    }

    /// <summary>The predicate, inverted.</summary>
    internal static Expression<Func<T, bool>> Negate<T>(Expression<Func<T, bool>> predicate) =>
        Expression.Lambda<Func<T, bool>>(Expression.Not(predicate.Body), predicate.Parameters);

    private sealed class ParameterReplacer(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == parameter ? replacement : base.VisitParameter(node);
    }
}
