namespace i26.Cqrs;

/// <summary>
/// A request that reads and changes nothing.
/// </summary>
/// <typeparam name="TResponse">What the query answers with.</typeparam>
/// <remarks>
/// <code>
/// public sealed record GetCourseQuery(CourseId Id) : IQuery&lt;CourseResponse&gt;;
/// </code>
/// </remarks>
public interface IQuery<TResponse>;
