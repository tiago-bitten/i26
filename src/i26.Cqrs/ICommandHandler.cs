using i26.Core.Results;

namespace i26.Cqrs;

/// <summary>Handles a command that answers with success or failure.</summary>
/// <typeparam name="TCommand">The command it handles.</typeparam>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    /// <summary>Runs the command.</summary>
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Handles a command that answers with a value.</summary>
/// <typeparam name="TCommand">The command it handles.</typeparam>
/// <typeparam name="TResponse">What the command produces on success.</typeparam>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>Runs the command.</summary>
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
