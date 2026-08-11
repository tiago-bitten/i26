using System.Diagnostics.CodeAnalysis;

namespace i26.Core.Results;

/// <summary>
/// The outcome of an operation: either success, or a failure described by an <see cref="Error"/>.
/// </summary>
/// <remarks>
/// <para>
/// Business rules return a result — they do not throw. Exceptions stay for what is genuinely
/// exceptional (a broken dependency, a bug), and the expected failure paths stay visible in the
/// signature:
/// </para>
/// <code>
/// // no
/// throw new NotFoundException("Course not found");
///
/// // yes — the implicit conversion from Error does the work
/// return CourseErrors.NotFound;
/// </code>
/// </remarks>
public class Result
{
    /// <summary>Creates a result.</summary>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="error">
    /// <see cref="Error.None"/> on success, the failure otherwise.
    /// </param>
    /// <exception cref="ArgumentException">
    /// A successful result carries an error, or a failed one carries <see cref="Error.None"/>.
    /// </exception>
    protected Result(bool isSuccess, Error error)
    {
        if ((isSuccess && error != Error.None) || (!isSuccess && error == Error.None))
        {
            throw new ArgumentException("Invalid error", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The failure, or <see cref="Error.None"/> when this result is a success.</summary>
    public Error Error { get; }

    /// <summary>HTTP status code for this outcome: 200 on success, the error's status otherwise.</summary>
    public int StatusCode => IsSuccess ? 200 : Error.StatusCode;

    /// <summary>Creates a successful result.</summary>
    /// <returns>The result.</returns>
    public static Result Ok() => new(true, Error.None);

    /// <summary>Creates a successful result carrying a value.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    public static Result<TValue> Ok<TValue>(TValue value) => new(true, value, Error.None);

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The result.</returns>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>Creates a failed result of a value-carrying type.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="error">The failure.</param>
    /// <returns>The result.</returns>
    public static Result<TValue> Failure<TValue>(Error error) => new(false, default!, error);

    /// <summary>Turns an error into a failed result, so <c>return SomeErrors.NotFound;</c> compiles.</summary>
    /// <param name="error">The failure.</param>
    public static implicit operator Result(Error error) => new(false, error);
}

/// <summary>
/// The outcome of an operation that produces a value.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public class Result<T> : Result
{
    private readonly T? _value;

    /// <summary>Creates a result.</summary>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="value">The value on success; ignored otherwise.</param>
    /// <param name="error">
    /// <see cref="Error.None"/> on success, the failure otherwise.
    /// </param>
    public Result(bool isSuccess, T? value, Error error)
        : base(isSuccess, error)
        => _value = value;

    /// <summary>The value produced by the operation.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    /// <remarks>
    /// Check <see cref="Result.IsFailure"/> and return the error before reaching for this; do not
    /// read it inline, give it a name:
    /// <code>
    /// var userResult = User.Create(name, email);
    /// if (userResult.IsFailure)
    /// {
    ///     return userResult.Error;
    /// }
    ///
    /// var user = userResult.Value;
    /// </code>
    /// </remarks>
    [NotNull]
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value is not available.");

    /// <summary>Creates a failed result whose error describes a validation problem.</summary>
    /// <param name="error">The validation failure.</param>
    /// <returns>The result.</returns>
    public static Result<T> ValidationFailure(Error error) => new(false, default, error);

    /// <summary>Turns an error into a failed result.</summary>
    /// <param name="error">The failure.</param>
    public static implicit operator Result<T>(Error error) => new(false, default!, error);

    /// <summary>Turns a value into a successful result, so <c>return user;</c> compiles.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator Result<T>(T value) => new(true, value, Error.None);
}
