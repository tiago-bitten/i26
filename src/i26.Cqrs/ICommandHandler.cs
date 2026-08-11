using i26.Core.Results;

namespace i26.Cqrs;

/// <summary>
/// Handles a command that answers with nothing but success or failure.
/// </summary>
/// <typeparam name="TCommand">The command it handles.</typeparam>
/// <remarks>
/// There is no mediator in the middle: the caller asks the container for the handler of the exact
/// command it wants, which the compiler checks and a reader can follow.
/// <code>
/// internal sealed class PublishCourseHandler(ICourseRepository courses)
///     : ICommandHandler&lt;PublishCourseCommand&gt;
/// {
///     public async Task&lt;Result&gt; HandleAsync(PublishCourseCommand command, CancellationToken ct = default)
///     {
///         var course = await courses.FindAsync(command.Id, ct);
///
///         if (course is null)
///         {
///             return CourseErrors.NotFound;
///         }
///
///         return course.Publish();
///     }
/// }
/// </code>
/// </remarks>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="command">What to do.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or the failure that stopped it.</returns>
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles a command that answers with a value.
/// </summary>
/// <typeparam name="TCommand">The command it handles.</typeparam>
/// <typeparam name="TResponse">What the command produces on success.</typeparam>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>Runs the command.</summary>
    /// <param name="command">What to do.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The value it produced, or the failure that stopped it.</returns>
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
