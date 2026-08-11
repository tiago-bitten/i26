using i26.Core.Results;

namespace i26.Core.Tests.Results;

public class ErrorTests
{
    /// <summary>
    /// Written out by hand on purpose: it is a second, independent copy of the mapping, so a typo
    /// in <see cref="Error.StatusCode"/> shows up as a failing test instead of a wrong response.
    /// </summary>
    private static readonly Dictionary<ErrorType, int> ExpectedStatusCodes = new()
    {
        [ErrorType.Validation] = 400,
        [ErrorType.Problem] = 400,
        [ErrorType.Unauthorized] = 401,
        [ErrorType.PaymentRequired] = 402,
        [ErrorType.Forbidden] = 403,
        [ErrorType.NotFound] = 404,
        [ErrorType.MethodNotAllowed] = 405,
        [ErrorType.NotAcceptable] = 406,
        [ErrorType.ProxyAuthenticationRequired] = 407,
        [ErrorType.RequestTimeout] = 408,
        [ErrorType.Conflict] = 409,
        [ErrorType.Gone] = 410,
        [ErrorType.LengthRequired] = 411,
        [ErrorType.PreconditionFailed] = 412,
        [ErrorType.ContentTooLarge] = 413,
        [ErrorType.UriTooLong] = 414,
        [ErrorType.UnsupportedMediaType] = 415,
        [ErrorType.RangeNotSatisfiable] = 416,
        [ErrorType.ExpectationFailed] = 417,
        [ErrorType.MisdirectedRequest] = 421,
        [ErrorType.UnprocessableContent] = 422,
        [ErrorType.Locked] = 423,
        [ErrorType.FailedDependency] = 424,
        [ErrorType.TooEarly] = 425,
        [ErrorType.UpgradeRequired] = 426,
        [ErrorType.PreconditionRequired] = 428,
        [ErrorType.TooManyRequests] = 429,
        [ErrorType.RequestHeaderFieldsTooLarge] = 431,
        [ErrorType.UnavailableForLegalReasons] = 451,
        [ErrorType.Failure] = 500,
        [ErrorType.NotImplemented] = 501,
        [ErrorType.BadGateway] = 502,
        [ErrorType.ServiceUnavailable] = 503,
        [ErrorType.GatewayTimeout] = 504,
        [ErrorType.HttpVersionNotSupported] = 505,
        [ErrorType.VariantAlsoNegotiates] = 506,
        [ErrorType.InsufficientStorage] = 507,
        [ErrorType.LoopDetected] = 508,
        [ErrorType.NotExtended] = 510,
        [ErrorType.NetworkAuthenticationRequired] = 511,
    };

    [Fact]
    public void Every_error_type_maps_to_its_status_code()
    {
        var declared = Enum.GetValues<ErrorType>();

        // Adding a member to the enum without an arm in the switch fails right here.
        Assert.Equal(ExpectedStatusCodes.Count, declared.Length);

        foreach (var type in declared)
        {
            Assert.Equal(ExpectedStatusCodes[type], new Error("some.code", type).StatusCode);
        }
    }

    [Fact]
    public void Every_status_code_is_a_client_or_server_error()
    {
        foreach (var type in Enum.GetValues<ErrorType>())
        {
            Assert.InRange(new Error("some.code", type).StatusCode, 400, 599);
        }
    }

    [Fact]
    public void The_first_six_types_keep_the_ordinals_they_shipped_with()
    {
        // They are part of the contract of anything that persisted or serialized an error type.
        Assert.Equal(0, (int)ErrorType.Failure);
        Assert.Equal(1, (int)ErrorType.Validation);
        Assert.Equal(2, (int)ErrorType.Problem);
        Assert.Equal(3, (int)ErrorType.NotFound);
        Assert.Equal(4, (int)ErrorType.Conflict);
        Assert.Equal(5, (int)ErrorType.Forbidden);
    }

    [Fact]
    public void A_value_cast_in_from_outside_the_enum_falls_back_to_500()
    {
        Assert.Equal(500, new Error("some.code", (ErrorType)999).StatusCode);
    }

    [Fact]
    public void Factories_set_the_code_and_the_type_and_carry_no_arguments()
    {
        Assert.Equal(new Error("a.b", ErrorType.Failure), Error.Failure("a.b"));
        Assert.Equal(new Error("a.b", ErrorType.NotFound), Error.NotFound("a.b"));
        Assert.Equal(new Error("a.b", ErrorType.Problem), Error.Problem("a.b"));
        Assert.Equal(new Error("a.b", ErrorType.Conflict), Error.Conflict("a.b"));
        Assert.Equal(new Error("a.b", ErrorType.Validation), Error.Validation("a.b"));
        Assert.Equal(new Error("a.b", ErrorType.Forbidden), Error.Forbidden("a.b"));

        Assert.Null(Error.NotFound("a.b").Arguments);
        Assert.Null(Error.NotFound("a.b").Metadata);
    }

    [Fact]
    public void The_added_factories_carry_their_own_status()
    {
        Assert.Equal(401, Error.Unauthorized("session.expired").StatusCode);
        Assert.Equal(402, Error.PaymentRequired("plan.overdue").StatusCode);
        Assert.Equal(410, Error.Gone("course.archived").StatusCode);
        Assert.Equal(422, Error.UnprocessableContent("import.rowsInconsistent").StatusCode);
        Assert.Equal(429, Error.TooManyRequests("ai.rateLimited").StatusCode);
        Assert.Equal(503, Error.ServiceUnavailable("ai.providerDown").StatusCode);
    }

    [Fact]
    public void Create_reaches_the_types_without_a_factory()
    {
        var error = Error.Create("upstream.badGateway", ErrorType.BadGateway);

        Assert.Equal(502, error.StatusCode);
        Assert.Equal("upstream.badGateway", error.Code);
    }

    [Fact]
    public void A_factory_takes_the_values_the_message_needs()
    {
        var error = Error.Validation("course.title.tooLong", 200);

        Assert.Equal([200], error.Arguments);
        Assert.Equal("course.title.tooLong", error.Code);
    }

    [Fact]
    public void WithArguments_attaches_the_values_to_a_copy()
    {
        var canonical = Error.Validation("course.title.tooLong");

        var parameterized = canonical.WithArguments(200, "title");

        Assert.Null(canonical.Arguments);
        Assert.Equal([200, "title"], parameterized.Arguments);
    }

    [Fact]
    public void WithMetadata_attaches_the_bag_to_a_copy()
    {
        var error = Error.Failure("ai.fallbackFailed");

        var withMetadata = error.WithMetadata(new Dictionary<string, object?> { ["model"] = "sonnet" });

        Assert.Null(error.Metadata);
        Assert.Equal("sonnet", withMetadata.Metadata!["model"]);
        Assert.Equal(error.Code, withMetadata.Code);
    }

    [Fact]
    public void None_is_empty_and_NullValue_has_a_code()
    {
        Assert.Empty(Error.None.Code);
        Assert.Equal("general.null", Error.NullValue.Code);
    }

    [Fact]
    public void Identity_is_the_code_and_the_type()
    {
        Assert.Equal(Error.NotFound("course.notFound"), Error.NotFound("course.notFound"));
        Assert.NotEqual(Error.NotFound("course.notFound"), Error.NotFound("course.gone"));
        Assert.NotEqual(Error.NotFound("course.notFound"), Error.Conflict("course.notFound"));
    }

    [Fact]
    public void Arguments_and_metadata_do_not_change_what_an_error_is()
    {
        // This is the whole point of keeping the text out: an error still matches the canonical one
        // it came from, no matter what it is carrying.
        var canonical = Error.Validation("course.title.tooLong");

        Assert.Equal(canonical, Error.Validation("course.title.tooLong", 200));
        Assert.Equal(canonical, canonical.WithArguments(500));
        Assert.Equal(canonical, canonical.WithMetadata(new Dictionary<string, object?> { ["a"] = 1 }));

        Assert.Equal(canonical.GetHashCode(), canonical.WithArguments(200).GetHashCode());
    }

    [Fact]
    public void An_error_can_be_matched_against_the_canonical_one_after_travelling()
    {
        Result<int> result = CourseErrors.TitleTooLong(200);

        Assert.Equal(CourseErrors.TitleTooLong(500), result.Error);
        Assert.True(result.Error == CourseErrors.TitleTooLong(200));
        Assert.NotEqual(CourseErrors.NotFound, result.Error);
    }

    [Fact]
    public void An_error_works_as_a_dictionary_key()
    {
        var counters = new Dictionary<Error, int>
        {
            [CourseErrors.NotFound] = 1,
        };

        counters[CourseErrors.NotFound]++;
        counters[Error.NotFound("course.notFound")]++;

        Assert.Equal(3, counters[CourseErrors.NotFound]);
        Assert.Single(counters);
    }

    /// <summary>The shape error codes are meant to be declared in.</summary>
    private static class CourseErrors
    {
        public static readonly Error NotFound = Error.NotFound("course.notFound");

        public static Error TitleTooLong(int max) => Error.Validation("course.title.tooLong", max);
    }
}
