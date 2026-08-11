using System.Text.Json;
using i26.AspNetCore.Diagnostics;
using i26.Core.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace i26.AspNetCore.Tests.Diagnostics;

public class GlobalExceptionHandlerTests
{
    private static async Task<(bool Handled, int StatusCode, string? ContentType, JsonElement? Body)> HandleAsync(
        Exception exception,
        // Environments.Production is a static readonly, so it cannot be a default value.
        string environmentName = "Production",
        IErrorTranslator? translator = null,
        Action<DefaultHttpContext>? configureContext = null)
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
        configureContext?.Invoke(httpContext);

        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            new StubEnvironment(environmentName));

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        if (body.Length == 0)
        {
            return (handled, httpContext.Response.StatusCode, httpContext.Response.ContentType, null);
        }

        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);

        return (handled, httpContext.Response.StatusCode, httpContext.Response.ContentType, document.RootElement.Clone());
    }

    private static BadHttpRequestException MalformedField(string path) =>
        new("Failed to read parameter from the request body.", new JsonException("not a number", path, 1, 12));

    [Fact]
    public async Task A_malformed_field_becomes_a_400_naming_the_field()
    {
        var (handled, statusCode, _, body) = await HandleAsync(MalformedField("$.classroomId"));

        Assert.True(handled);
        Assert.Equal(400, statusCode);
        Assert.Equal("request.classroomId.invalid", body!.Value.GetProperty("code").GetString());
        Assert.Equal("request.classroomId.invalid", body.Value.GetProperty("title").GetString());
    }

    [Fact]
    public async Task A_nested_field_keeps_its_whole_path_in_the_code()
    {
        var (_, _, _, body) = await HandleAsync(MalformedField("$.address.postalCode"));

        Assert.Equal("request.address.postalCode.invalid", body!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_field_detail_is_answered_even_outside_development()
    {
        // It describes the payload the caller sent; there is nothing of ours in it.
        var (_, _, _, body) = await HandleAsync(MalformedField("$.classroomId"));

        Assert.Contains("classroomId", body!.Value.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bad_request_without_a_field_falls_back_to_the_body_code()
    {
        var (_, statusCode, _, body) = await HandleAsync(new BadHttpRequestException("Request body too large."));

        Assert.Equal(400, statusCode);
        Assert.Equal("request.body.invalid", body!.Value.GetProperty("code").GetString());
        Assert.Equal("Request body too large.", body.Value.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Anything_else_becomes_a_500()
    {
        var (handled, statusCode, _, body) = await HandleAsync(new InvalidOperationException("boom"));

        Assert.True(handled);
        Assert.Equal(500, statusCode);
        Assert.Equal("general.failure", body!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_exception_message_never_reaches_the_client_in_production()
    {
        var exception = new InvalidOperationException("Host=db.internal;Password=hunter2");

        var (_, _, _, body) = await HandleAsync(exception);

        Assert.False(body!.Value.TryGetProperty("detail", out _));
        Assert.DoesNotContain("hunter2", body.Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_exception_message_is_answered_in_development()
    {
        var (_, _, _, body) = await HandleAsync(
            new InvalidOperationException("boom"),
            Environments.Development);

        Assert.Equal("boom", body!.Value.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task It_answers_with_the_problem_media_type_and_the_specification_link()
    {
        var (_, _, contentType, body) = await HandleAsync(new InvalidOperationException("boom"));

        Assert.StartsWith("application/problem+json", contentType!, StringComparison.Ordinal);
        Assert.Equal(
            "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            body!.Value.GetProperty("type").GetString());
    }

    [Fact]
    public async Task A_translator_localizes_the_code_and_wins_over_the_message()
    {
        var (_, _, _, body) = await HandleAsync(
            new InvalidOperationException("boom"),
            Environments.Development,
            new StubTranslator("Something went wrong on our side"));

        Assert.Equal("Something went wrong on our side", body!.Value.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task The_field_reaches_the_translator_as_an_argument()
    {
        var translator = new CapturingTranslator();

        await HandleAsync(MalformedField("$.classroomId"), translator: translator);

        Assert.Equal("request.classroomId.invalid", translator.Seen!.Code);
        Assert.Equal(["classroomId"], translator.Seen.Arguments);
    }

    [Fact]
    public async Task An_aborted_request_is_swallowed_without_a_response()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        var (handled, _, _, body) = await HandleAsync(
            new OperationCanceledException(),
            configureContext: context => context.RequestAborted = aborted.Token);

        Assert.True(handled);
        Assert.Null(body);
    }

    [Fact]
    public async Task A_cancellation_that_is_not_an_abort_is_still_a_failure()
    {
        var (_, statusCode, _, body) = await HandleAsync(new OperationCanceledException());

        Assert.Equal(500, statusCode);
        Assert.Equal("general.failure", body!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task It_gives_up_when_the_response_has_already_started()
    {
        var (handled, _, _, body) = await HandleAsync(
            new InvalidOperationException("boom"),
            configureContext: context => context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature()));

        Assert.False(handled);
        Assert.Null(body);
    }

    private sealed class StubTranslator(string description) : IErrorTranslator
    {
        public string? Describe(Error error) => description;
    }

    private sealed class CapturingTranslator : IErrorTranslator
    {
        public Error? Seen { get; private set; }

        public string? Describe(Error error)
        {
            Seen = error;
            return null;
        }
    }

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
