namespace i26.Cqrs;

/// <summary>
/// A request that changes something and answers with nothing but success or failure.
/// </summary>
/// <remarks>
/// A marker: it carries the intent and the data, and nothing else. The work belongs to its
/// <see cref="ICommandHandler{TCommand}"/>.
/// <code>
/// public sealed record PublishCourseCommand(CourseId Id) : ICommand;
/// </code>
/// </remarks>
public interface ICommand;

/// <summary>
/// A request that changes something and answers with a value.
/// </summary>
/// <typeparam name="TResponse">What the command produces on success.</typeparam>
/// <remarks>
/// The response type is part of the command, so the handler interface and every call site agree on
/// it without anyone repeating themselves:
/// <code>
/// public sealed record CreateCourseCommand(string Title) : ICommand&lt;CourseId&gt;;
/// </code>
/// </remarks>
public interface ICommand<TResponse>;
