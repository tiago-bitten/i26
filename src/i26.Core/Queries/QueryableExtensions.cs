using System.Linq.Expressions;
using i26.Core.Specifications;

namespace i26.Core.Queries;

/// <summary>Filtering a query by a rule, or by a rule that may not apply.</summary>
public static class QueryableExtensions
{
    /// <summary>The rows that satisfy the specification.</summary>
    public static IQueryable<T> Where<T>(this IQueryable<T> query, ISpecification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(specification);

        return query.Where(specification.ToExpression());
    }

    /// <summary>The rows that match, when the condition holds; every row otherwise.</summary>
    /// <remarks>The condition is about the request, not about a row, and never reaches the database.</remarks>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> query,
        bool condition,
        Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(query);

        return condition ? query.Where(predicate) : query;
    }

    /// <summary>The rows that satisfy the specification, when the condition holds.</summary>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> query,
        bool condition,
        ISpecification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(query);

        return condition ? query.Where(specification) : query;
    }

    /// <summary>The items that satisfy the specification.</summary>
    public static IEnumerable<T> Where<T>(this IEnumerable<T> source, ISpecification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(specification);

        return source.Where(specification.IsSatisfiedBy);
    }

    /// <summary>The items that match, when the condition holds; every item otherwise.</summary>
    public static IEnumerable<T> WhereIf<T>(this IEnumerable<T> source, bool condition, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);

        return condition ? source.Where(predicate) : source;
    }
}
