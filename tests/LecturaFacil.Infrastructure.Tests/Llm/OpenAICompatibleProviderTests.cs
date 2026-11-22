// SPDX-License-Identifier: Apache-2.0
using System.Net;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;
using LecturaFacil.Infrastructure.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace LecturaFacil.Infrastructure.Tests.Llm;

public sealed class OpenAICompatibleProviderTests
{
    private const string Endpoint = "http://localhost:11434/v1";
    private static readonly LimitsOptions Limits = new() { MaxOutputTokens = 1_000, LlmTimeoutSeconds = 30 };

    [Fact]
    public async Task Complete_SendsAChatCompletionToTheConfiguredEndpointWithTheKeyInTheAuthorizationHeader()
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""");

        await CompleteAsync(handler, Request(LlmRole.Adapter));

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.ShouldBe(new Uri("http://localhost:11434/v1/chat/completions"));
        request.Headers["Authorization"].ShouldBe($"Bearer {LlmTestOptions.ApiKey}");
        request.Uri.ToString().ShouldNotContain(LlmTestOptions.ApiKey);
        request.Body.ShouldNotContain(LlmTestOptions.ApiKey);
    }

    [Fact]
    public async Task Complete_WhenEndpointEndsWithSlash_DoesNotDuplicateIt()
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""");

        await CompleteAsync(handler, Request(LlmRole.Adapter), endpoint: "http://localhost:11434/v1/");

        handler.Requests.ShouldHaveSingleItem().Uri.ShouldBe(new Uri("http://localhost:11434/v1/chat/completions"));
    }

    [Theory]
    [InlineData(LlmRole.Adapter, "despliegue-adaptador")]
    [InlineData(LlmRole.Verifier, "despliegue-verificador")]
    public async Task Complete_UsesTheModelConfiguredForTheRole(LlmRole role, string expectedModel)
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""");

        await CompleteAsync(handler, Request(role));

        handler.Requests.ShouldHaveSingleItem().Json.GetProperty("model").GetString().ShouldBe(expectedModel);
    }

    [Fact]
    public async Task Complete_SendsSystemAndUserMessagesJsonSchemaTemperatureAndNoTools()
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""");

        await CompleteAsync(handler, Request(LlmRole.Adapter), temperature: 0.2);

        var body = handler.Requests.ShouldHaveSingleItem().Json;
        var messages = body.GetProperty("messages").EnumerateArray().ToArray();
        messages.Select(message => message.GetProperty("role").GetString()).ShouldBe(["system", "user"]);
        messages[0].GetProperty("content").ToString().ShouldContain("sistema");
        messages[1].GetProperty("content").ToString().ShouldContain("usuario");
        body.GetProperty("temperature").GetDouble().ShouldBe(0.2, tolerance: 0.0001);
        var format = body.GetProperty("response_format");
        format.GetProperty("type").GetString().ShouldBe("json_schema");
        format.GetProperty("json_schema").GetProperty("schema").GetProperty("required")
            .EnumerateArray().Select(item => item.GetString()).ShouldBe(["ok"]);
        body.TryGetProperty("tools", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Complete_WhenThinkingIsDisabled_AsksTheChatTemplateNotToThink()
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""");

        await CompleteAsync(handler, Request(LlmRole.Adapter), disableThinking: true);

        var body = handler.Requests.ShouldHaveSingleItem().Json;
        body.GetProperty("chat_template_kwargs").GetProperty("enable_thinking").GetBoolean().ShouldBeFalse();
        body.GetProperty("response_format").GetProperty("type").GetString().ShouldBe("json_schema");
    }

    [Fact]
    public async Task Complete_ByDefault_DoesNotSendChatTemplateArguments()
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""");

        await CompleteAsync(handler, Request(LlmRole.Adapter));

        handler.Requests.ShouldHaveSingleItem().Json.TryGetProperty("chat_template_kwargs", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(200, 200)]
    [InlineData(5_000, 1_000)]
    public async Task Complete_NeverAsksForMoreTokensThanTheLimit(int requested, int expected)
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""");

        await CompleteAsync(handler, new LlmRequest(LlmRole.Adapter, "sistema", "usuario", requested));

        var body = handler.Requests.ShouldHaveSingleItem().Json;
        var maxTokens = body.TryGetProperty("max_completion_tokens", out var value) ? value : body.GetProperty("max_tokens");
        maxTokens.GetInt32().ShouldBe(expected);
    }

    [Fact]
    public async Task Complete_WhenTheResponseMatchesTheSchema_ReturnsTheValueAndWhoAnswered()
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""", model: "despliegue-adaptador");

        var result = await CompleteAsync(handler, Request(LlmRole.Adapter));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(new PingReply(true));
        result.Value.Provider.ShouldBe("Servidor de pruebas");
        result.Value.Model.ShouldBe("despliegue-adaptador");
        result.Value.InputTokens.ShouldBe(120);
        result.Value.OutputTokens.ShouldBe(7);
        result.Value.Duration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Theory]
    [InlineData("""{"ok":"sí"}""")]
    [InlineData("""{"ok":tr""")]
    [InlineData("No puedo responder a eso.")]
    [InlineData("")]
    public async Task Complete_WhenTheResponseDoesNotMatchTheSchema_ReturnsInvalidProviderOutput(string content)
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion(content);

        var result = await CompleteAsync(handler, Request(LlmRole.Adapter));

        result.Error.ShouldBe(LlmErrors.InvalidProviderOutput);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Complete_WhenTheProviderAnswersWithAnError_ReturnsProviderUnavailable(HttpStatusCode status)
    {
        var handler = FakeHttpMessageHandler.Returning(status, """{"error":{"message":"fallo interno"}}""");

        var result = await CompleteAsync(handler, Request(LlmRole.Adapter));

        result.Error.ShouldBe(LlmErrors.ProviderUnavailable);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Complete_WhenTheProviderIsUnreachable_ReturnsProviderUnavailable()
    {
        var handler = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("Connection refused"));

        var result = await CompleteAsync(handler, Request(LlmRole.Adapter));

        result.Error.ShouldBe(LlmErrors.ProviderUnavailable);
    }

    [Fact]
    public async Task Complete_WhenTheProviderDoesNotAnswerInTime_ReturnsProviderUnavailable()
    {
        var handler = new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        });
        var limits = new LimitsOptions { LlmTimeoutSeconds = 5 };

        var result = await CompleteAsync(handler, Request(LlmRole.Adapter), limits: limits, timeoutOverride: TimeSpan.FromMilliseconds(200));

        result.Error.ShouldBe(LlmErrors.ProviderUnavailable);
    }

    [Fact]
    public async Task Complete_WhenTheProviderAnswers200WithSomethingThatIsNotAChatCompletion_ReturnsProviderUnavailable()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "<html><body>Página del proxy</body></html>");

        var result = await CompleteAsync(handler, Request(LlmRole.Adapter));

        result.Error.ShouldBe(LlmErrors.ProviderUnavailable);
    }

    [Theory]
    [InlineData("length")]
    [InlineData("content_filter")]
    public async Task Complete_WhenTheResponseDidNotFinishNormally_ReturnsInvalidProviderOutputEvenIfItIsValidJson(string finishReason)
    {
        var body = FakeHttpMessageHandler.ChatCompletion("""{"ok":true}""", "modelo").Replace("\"stop\"", $"\"{finishReason}\"", StringComparison.Ordinal);
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, body);

        var result = await CompleteAsync(handler, Request(LlmRole.Adapter));

        result.Error.ShouldBe(LlmErrors.InvalidProviderOutput);
    }

    [Fact]
    public async Task Complete_WhenTheDocumentExceedsTheCharacterLimit_ReturnsInputTooLongWithoutCallingTheProvider()
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""");
        var limits = new LimitsOptions { MaxCharacters = 100 };
        var prompt = new Prompt("prueba.v1", $"Documento entre <{LlmRequest.DocumentTagPlaceholder}>.");

        var atLimit = await CompleteAsync(handler, LlmRequest.ForDocument(LlmRole.Adapter, prompt, new string('a', 100), 200), limits: limits);
        var overLimit = await CompleteAsync(handler, LlmRequest.ForDocument(LlmRole.Adapter, prompt, new string('a', 101), 200), limits: limits);

        atLimit.IsSuccess.ShouldBeTrue();
        overLimit.Error.ShouldBe(LlmErrors.InputTooLong);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Complete_WhenAComparisonFitsTheVerificationLimit_SendsIt()
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""");
        var limits = new LimitsOptions { MaxCharacters = 100, MaxCharactersPerVerification = 1_000 };
        var prompt = new Prompt("prueba.v1", $"<{LlmRequest.OriginalTagPlaceholder}> <{LlmRequest.AdaptationTagPlaceholder}>");

        var atLimit = await CompleteAsync(
            handler, LlmRequest.ForComparison(LlmRole.Verifier, prompt, new string('a', 600), new string('b', 400), 200), limits: limits);
        var overLimit = await CompleteAsync(
            handler, LlmRequest.ForComparison(LlmRole.Verifier, prompt, new string('a', 600), new string('b', 401), 200), limits: limits);

        atLimit.IsSuccess.ShouldBeTrue();
        overLimit.Error.ShouldBe(LlmErrors.InputTooLong);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Complete_WhenTheCallerCancels_Throws()
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => CompleteAsync(handler, Request(LlmRole.Adapter), cancellationToken: cancellation.Token));
    }

    private static LlmRequest Request(LlmRole role) => new(role, "mensaje de sistema", "mensaje de usuario", 200);

    private static async Task<Result<LlmResponse<PingReply>>> CompleteAsync(
        FakeHttpMessageHandler handler,
        LlmRequest request,
        string endpoint = Endpoint,
        double temperature = 0,
        bool disableThinking = false,
        LimitsOptions? limits = null,
        TimeSpan? timeoutOverride = null,
        CancellationToken? cancellationToken = null)
    {
        var options = LlmTestOptions.Provider(LlmProviderType.OpenAICompatible, endpoint);
        options.Temperature = temperature;
        options.DisableThinking = disableThinking;
        using var httpClient = new HttpClient(handler) { Timeout = timeoutOverride ?? Timeout.InfiniteTimeSpan };
        var provider = new OpenAICompatibleProvider(options, httpClient, limits ?? Limits, NullLogger.Instance);
        return await provider.CompleteStructuredAsync<PingReply>(request, cancellationToken ?? TestContext.Current.CancellationToken);
    }
}
