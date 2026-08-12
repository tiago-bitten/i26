using System.Linq.Expressions;
using i26.Core.Queries;
using i26.Core.Specifications;

namespace i26.Core.Tests.Specifications;

/// <summary>
/// A specification has to answer the same thing twice — once in memory and once as an expression a
/// provider reads — so the tests come in pairs, and the composition tests look at the tree itself.
/// </summary>
public sealed class SpecificationTests
{
    private sealed record Course(string Title, bool IsPublished, int Students);

    private sealed class Published : Specification<Course>
    {
        public override Expression<Func<Course, bool>> ToExpression() => course => course.IsPublished;
    }

    private sealed class Popular(int atLeast) : Specification<Course>
    {
        public override Expression<Func<Course, bool>> ToExpression() => course => course.Students >= atLeast;
    }

    /// <summary>Implements the interface without inheriting, which composition has to accept too.</summary>
    private sealed class Titled(string title) : ISpecification<Course>
    {
        public Expression<Func<Course, bool>> ToExpression() => course => course.Title == title;

        public bool IsSatisfiedBy(Course candidate) => ToExpression().Compile()(candidate);
    }

    private static readonly Course Algebra = new("Algebra", IsPublished: true, Students: 30);
    private static readonly Course Draft = new("Draft", IsPublished: false, Students: 0);
    private static readonly Course Empty = new("Empty", IsPublished: true, Students: 2);

    private static IQueryable<Course> Courses => new[] { Algebra, Draft, Empty }.AsQueryable();

    [Fact]
    public void A_specification_answers_in_memory()
    {
        var published = new Published();

        Assert.True(published.IsSatisfiedBy(Algebra));
        Assert.False(published.IsSatisfiedBy(Draft));
    }

    [Fact]
    public void The_compiled_form_is_kept_rather_than_built_again()
    {
        var counting = new CountingSpecification();

        counting.IsSatisfiedBy(Algebra);
        counting.IsSatisfiedBy(Draft);
        counting.IsSatisfiedBy(Empty);

        Assert.Equal(1, counting.Built);
    }

    [Fact]
    public void And_asks_both()
    {
        var specification = new Published().And(new Popular(10));

        Assert.True(specification.IsSatisfiedBy(Algebra));
        Assert.False(specification.IsSatisfiedBy(Empty));
        Assert.False(specification.IsSatisfiedBy(Draft));
    }

    [Fact]
    public void Or_asks_either()
    {
        var specification = new Popular(10).Or(new Published());

        Assert.True(specification.IsSatisfiedBy(Algebra));
        Assert.True(specification.IsSatisfiedBy(Empty));
        Assert.False(specification.IsSatisfiedBy(Draft));
    }

    [Fact]
    public void Not_inverts()
    {
        var specification = new Published().Not();

        Assert.True(specification.IsSatisfiedBy(Draft));
        Assert.False(specification.IsSatisfiedBy(Algebra));
    }

    [Fact]
    public void A_rule_that_only_implements_the_interface_composes_too()
    {
        var specification = new Titled("Algebra").And(new Published());

        Assert.True(specification.IsSatisfiedBy(Algebra));
        Assert.False(specification.IsSatisfiedBy(Empty));
    }

    [Fact]
    public void Composition_nests()
    {
        var specification = new Published()
            .And(new Popular(10).Or(new Titled("Empty")))
            .Not();

        Assert.False(specification.IsSatisfiedBy(Algebra));
        Assert.False(specification.IsSatisfiedBy(Empty));
        Assert.True(specification.IsSatisfiedBy(Draft));
    }

    [Fact]
    public void Composing_leaves_one_parameter_and_no_invocation_behind()
    {
        var expression = new Published().And(new Popular(10)).ToExpression();

        // The tree a provider sees is the one it would see from `course => a && b`, which is the
        // whole reason the parameters are rebound instead of the lambdas invoked.
        Assert.Single(expression.Parameters);
        Assert.False(InvocationFinder.Any(expression));
        Assert.Equal(ExpressionType.AndAlso, expression.Body.NodeType);
    }

    [Fact]
    public void Filtering_a_query_by_a_specification()
    {
        Assert.Equal(
            [Algebra, Empty],
            Courses.Where(new Published()));

        Assert.Equal(
            [Algebra],
            Courses.Where(new Published().And(new Popular(10))));
    }

    [Fact]
    public void Filtering_a_list_by_a_specification()
    {
        IEnumerable<Course> courses = [Algebra, Draft, Empty];

        Assert.Equal([Algebra, Empty], courses.Where(new Published()));
    }

    [Fact]
    public void A_filter_that_does_not_apply_is_not_applied()
    {
        Assert.Equal(3, Courses.WhereIf(false, course => course.IsPublished).Count());
        Assert.Equal(2, Courses.WhereIf(true, course => course.IsPublished).Count());
        Assert.Equal(3, Courses.WhereIf(false, new Published()).Count());
        Assert.Equal(2, Courses.WhereIf(true, new Published()).Count());
    }

    [Fact]
    public void A_filter_that_does_not_apply_is_not_applied_in_memory_either()
    {
        IEnumerable<Course> courses = [Algebra, Draft, Empty];

        Assert.Equal(3, courses.WhereIf(false, course => course.IsPublished).Count());
        Assert.Equal(2, courses.WhereIf(true, course => course.IsPublished).Count());
    }

    [Fact]
    public void The_untouched_query_is_the_one_that_came_in()
    {
        var courses = Courses;

        Assert.Same(courses, courses.WhereIf(false, course => course.IsPublished));
    }

    [Fact]
    public void It_refuses_a_null_argument()
    {
        var published = new Published();

        Assert.Throws<ArgumentNullException>(() => published.And(null!));
        Assert.Throws<ArgumentNullException>(() => ((ISpecification<Course>)null!).Or(published));
        Assert.Throws<ArgumentNullException>(() => ((ISpecification<Course>)null!).Not());
        Assert.Throws<ArgumentNullException>(() => Courses.Where((ISpecification<Course>)null!));
        Assert.Throws<ArgumentNullException>(() => ((IQueryable<Course>)null!).Where(published));
        Assert.Throws<ArgumentNullException>(() => ((IQueryable<Course>)null!).WhereIf(true, published));
    }

    private sealed class CountingSpecification : Specification<Course>
    {
        public int Built { get; private set; }

        public override Expression<Func<Course, bool>> ToExpression()
        {
            Built++;

            return course => course.IsPublished;
        }
    }

    private sealed class InvocationFinder : ExpressionVisitor
    {
        private bool _found;

        public static bool Any(Expression expression)
        {
            var finder = new InvocationFinder();
            finder.Visit(expression);

            return finder._found;
        }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            _found = true;

            return base.VisitInvocation(node);
        }
    }
}
