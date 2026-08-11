using System.Globalization;
using System.Text.Json;
using i26.AspNetCore.Results;
using i26.Core.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace i26.AspNetCore.Tests.Results;

public class ProblemResultsTests
{
    private static readonly Error NotFound = Error.NotFound("course.notFound");

    private static async Task<(int StatusCode, JsonElement Body)> ExecuteAsync(
        IResult result,
        IErrorTranslator? translator = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        if (translator is not null)
        {
            services.AddSingleton(translator);
        }

        await using var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var body = new MemoryStream();
        httpContext.Response.Body = body;

        await result.ExecuteAsync(httpContext);

        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);

        return (httpContext.Response.StatusCode, document.RootElement.Clone());
    }

    [Fact]
    public void Problem_refuses_a_successful_result()
    {
        Assert.Throws<InvalidOperationException>(() => ProblemResults.Problem(Result.Ok()));
    }

    [Fact]
    public async Task The_status_comes_from_the_error_type()
    {
        var (statusCode, body) = await ExecuteAsync(ProblemResults.Problem(Result.Failure(NotFound)));

        Assert.Equal(404, statusCode);
        Assert.Equal(404, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task The_title_and_the_code_extension_carry_the_error_code()
    {
        var (_, body) = await ExecuteAsync(ProblemResults.Problem(Result.Failure(NotFound)));

        Assert.Equal("course.notFound", body.GetProperty("title").GetString());
        Assert.Equal("course.notFound", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_type_points_at_the_section_of_the_status_code()
    {
        var (_, body) = await ExecuteAsync(ProblemResults.Problem(Result.Failure(NotFound)));

        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.5", body.GetProperty("type").GetString());
        Assert.Equal(ProblemResults.GetTypeUri(404), body.GetProperty("type").GetString());
    }

    [Fact]
    public void Every_error_type_has_a_specification_section_to_point_at()
    {
        foreach (var type in Enum.GetValues<ErrorType>())
        {
            Assert.NotNull(ProblemResults.GetTypeUri(new Error("some.code", type).StatusCode));
        }
    }

    [Fact]
    public void An_unknown_status_has_no_section_and_the_member_is_left_out()
    {
        Assert.Null(ProblemResults.GetTypeUri(499));
        Assert.Null(ProblemResults.GetTypeUri(200));
    }

    [Fact]
    public async Task Every_error_type_reaches_the_response_with_its_own_status()
    {
        foreach (var type in Enum.GetValues<ErrorType>())
        {
            var error = new Error("some.code", type);

            var (statusCode, body) = await ExecuteAsync(ProblemResults.Problem(error));

            Assert.Equal(error.StatusCode, statusCode);
            Assert.Equal(error.StatusCode, body.GetProperty("status").GetInt32());
            Assert.Equal(ProblemResults.GetTypeUri(error.StatusCode), body.GetProperty("type").GetString());
        }
    }

    [Fact]
    public async Task The_detail_comes_from_the_registered_translator()
    {
        var (_, body) = await ExecuteAsync(
            ProblemResults.Problem(Result.Failure(NotFound)),
            new StubTranslator(("course.notFound", "Course not found")));

        Assert.Equal("Course not found", body.GetProperty("detail").GetString());
        Assert.Equal("course.notFound", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task The_translator_fills_the_placeholders_with_the_error_arguments()
    {
        var error = Error.Validation("course.title.tooLong", 200);

        var (_, body) = await ExecuteAsync(
            ProblemResults.Problem(error),
            new StubTranslator(("course.title.tooLong", "Title is longer than {0} characters")));

        Assert.Equal("Title is longer than 200 characters", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Without_a_translator_the_detail_member_is_left_out()
    {
        var (_, body) = await ExecuteAsync(ProblemResults.Problem(Result.Failure(NotFound)));

        Assert.False(body.TryGetProperty("detail", out _));
        Assert.Equal("course.notFound", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task A_code_the_translator_does_not_know_leaves_the_detail_out()
    {
        var (_, body) = await ExecuteAsync(
            ProblemResults.Problem(Result.Failure(NotFound)),
            new StubTranslator(("something.else", "Not this one")));

        Assert.False(body.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task A_validation_error_lists_every_individual_error()
    {
        var validationError = ValidationError.FromResults(
        [
            Result.Failure(Error.Validation("course.title.required")),
            Result.Failure(Error.Validation("course.price.invalid")),
        ]);

        var (statusCode, body) = await ExecuteAsync(ProblemResults.Problem(Result.Failure(validationError)));

        Assert.Equal(400, statusCode);

        var errors = body.GetProperty("errors").EnumerateArray().ToArray();

        Assert.Equal(2, errors.Length);
        Assert.Equal("course.title.required", errors[0].GetProperty("code").GetString());
        Assert.Equal("course.price.invalid", errors[1].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Each_error_inside_a_validation_error_is_described_on_its_own()
    {
        var validationError = ValidationError.FromResults(
        [
            Result.Failure(Error.Validation("course.title.required")),
            Result.Failure(Error.Validation("course.title.tooLong", 200)),
        ]);

        var (_, body) = await ExecuteAsync(
            ProblemResults.Problem(Result.Failure(validationError)),
            new StubTranslator(
                ("course.title.required", "Title is required"),
                ("course.title.tooLong", "Title is longer than {0} characters")));

        var errors = body.GetProperty("errors").EnumerateArray().ToArray();

        Assert.Equal("Title is required", errors[0].GetProperty("message").GetString());
        Assert.Equal("Title is longer than 200 characters", errors[1].GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_plain_error_has_no_errors_extension()
    {
        var (_, body) = await ExecuteAsync(ProblemResults.Problem(Result.Failure(NotFound)));

        Assert.False(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Metadata_never_reaches_the_response()
    {
        var error = Error.Failure("ai.fallbackFailed")
            .WithMetadata(new Dictionary<string, object?> { ["safetyModel"] = "internal-only" });

        var (_, body) = await ExecuteAsync(ProblemResults.Problem(error));

        Assert.DoesNotContain("internal-only", body.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("safetyModel", body.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Arguments_never_reach_the_response_on_their_own()
    {
        // They exist to fill a template, not to be shipped as data.
        var (_, body) = await ExecuteAsync(
            ProblemResults.Problem(Error.Validation("course.title.tooLong", "secret-limit")));

        Assert.DoesNotContain("secret-limit", body.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task It_plugs_into_Match_as_a_method_group()
    {
        Result<int> failed = NotFound;

        var httpResult = failed.Match(
            value => Microsoft.AspNetCore.Http.Results.Ok(value),
            ProblemResults.Problem);

        var (statusCode, _) = await ExecuteAsync(httpResult);

        Assert.Equal(404, statusCode);
    }

    /// <summary>Stands in for a resource-backed translator: a code to template lookup, plus formatting.</summary>
    private sealed class StubTranslator(params (string Code, string Template)[] entries) : IErrorTranslator
    {
        public string? Describe(Error error)
        {
            var template = Array.Find(entries, entry => entry.Code == error.Code).Template;

            if (template is null)
            {
                return null;
            }

            return error.Arguments is { Count: > 0 } arguments
                ? string.Format(CultureInfo.InvariantCulture, template, [.. arguments])
                : template;
        }
    }
}
