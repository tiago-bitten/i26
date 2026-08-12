using System.Diagnostics.CodeAnalysis;

namespace i26.Core.Results;

/// <summary>The outcome of an operation: success, or a failure described by an <see cref="Error"/>.</summary>
/// <remarks>
/// Business rules return a result rather than throwing, so the expected failures stay visible in the
/// signature. <c>return CourseErrors.NotFound;</c> works through the implicit conversion.
/// </remarks>
public class Result
{
    /// <summary>Creates a result.</summary>
    /// <exception cref="ArgumentException">
    /// A success carries an error, or a failure carries <see cref="Error.None"/>.
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

    /// <summary>The failure, or <see cref="Error.None"/> on success.</summary>
    public Error Error { get; }

    /// <summary>200 on success, the error's status otherwise.</summary>
    public int StatusCode => IsSuccess ? 200 : Error.StatusCode;

    /// <summary>Creates a successful result.</summary>
    public static Result Ok() => new(true, Error.None);

    /// <summary>Creates a successful result carrying a value.</summary>
    public static Result<TValue> Ok<TValue>(TValue value) => new(true, value, Error.None);

    /// <summary>Creates a failed result.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>Creates a failed result of a value-carrying type.</summary>
    public static Result<TValue> Failure<TValue>(Error error) => new(false, default!, error);

    /// <summary>Turns an error into a failed result, so <c>return CourseErrors.NotFound;</c> compiles.</summary>
    public static implicit operator Result(Error error) => new(false, error);
}

/// <summary>The outcome of an operation that produces a value.</summary>
/// <typeparam name="T">The value type.</typeparam>
public class Result<T> : Result
{
    private readonly T? _value;

    /// <summary>Creates a result.</summary>
    public Result(bool isSuccess, T? value, Error error)
        : base(isSuccess, error)
        => _value = value;

    /// <summary>The value produced by the operation.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    /// <remarks>Check <see cref="Result.IsFailure"/> and return the error before reaching for this.</remarks>
    [NotNull]
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value is not available.");

    /// <summary>Creates a failed result whose error describes a validation problem.</summary>
    public static Result<T> ValidationFailure(Error error) => new(false, default, error);

    /// <summary>Turns an error into a failed result.</summary>
    public static implicit operator Result<T>(Error error) => new(false, default!, error);

    /// <summary>Turns a value into a successful result, so <c>return course;</c> compiles.</summary>
    public static implicit operator Result<T>(T value) => new(true, value, Error.None);
}
