namespace i26.Cqrs;

/// <summary>A request that changes something and answers with success or failure.</summary>
/// <remarks>Handled by <see cref="ICommandHandler{TCommand}"/>.</remarks>
public interface ICommand;

/// <summary>A request that changes something and answers with a value.</summary>
/// <typeparam name="TResponse">What the command produces on success.</typeparam>
/// <remarks>
/// The response type lives on the command, so the handler and every call site agree on it without
/// restating it. Handled by <see cref="ICommandHandler{TCommand, TResponse}"/>.
/// </remarks>
public interface ICommand<TResponse>;
