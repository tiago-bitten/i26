using i26.Core.Results;

namespace i26.Core.Tests.Results;

public class ValidationErrorTests
{
    [Fact]
    public void FromResults_keeps_only_the_failures()
    {
        var titleRequired = Error.Validation("course.title.required");
        var priceInvalid = Error.Validation("course.price.invalid");

        var validationError = ValidationError.FromResults(
        [
            Result.Ok(),
            Result.Failure(titleRequired),
            Result.Ok(),
            Result.Failure(priceInvalid),
        ]);

        Assert.Equal([titleRequired, priceInvalid], validationError.Errors);
    }

    [Fact]
    public void FromResults_with_no_failure_produces_an_empty_list()
    {
        var validationError = ValidationError.FromResults([Result.Ok(), Result.Ok()]);

        Assert.Empty(validationError.Errors);
    }

    [Fact]
    public void It_is_a_validation_error_with_the_conventional_code()
    {
        var validationError = ValidationError.FromResults([]);

        Assert.Equal("validation.general", validationError.Code);
        Assert.Equal(ErrorType.Validation, validationError.Type);
        Assert.Equal(400, validationError.StatusCode);
    }

    [Fact]
    public void It_travels_as_the_error_of_a_result()
    {
        Result<int> result = ValidationError.FromResults([Result.Failure(Error.Validation("a.b"))]);

        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Single(error.Errors);
    }

    [Fact]
    public void Two_of_them_holding_the_same_failures_are_equal()
    {
        var first = ValidationError.FromResults([Result.Failure(Error.Validation("course.title.required"))]);
        var second = ValidationError.FromResults([Result.Failure(Error.Validation("course.title.required"))]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Holding_different_failures_tells_them_apart()
    {
        var first = ValidationError.FromResults([Result.Failure(Error.Validation("course.title.required"))]);
        var second = ValidationError.FromResults([Result.Failure(Error.Validation("course.price.invalid"))]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void It_is_not_the_same_as_a_plain_error_carrying_its_code()
    {
        Error plain = Error.Validation("validation.general");
        Error validationError = ValidationError.FromResults([]);

        Assert.NotEqual(plain, validationError);
        Assert.NotEqual(validationError, plain);
    }
}
