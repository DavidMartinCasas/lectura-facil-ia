// SPDX-License-Identifier: Apache-2.0
using System.Collections.Concurrent;
using System.Net;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;
using LecturaFacil.Infrastructure.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LecturaFacil.Infrastructure.Tests.Llm;

/// <summary>Proveedores tal y como los compone <see cref="DependencyInjection.AddInfrastructure"/>, sin red.</summary>
public sealed class LlmRegistrationTests
{
    [Theory]
    [InlineData(LlmProviderType.OpenAICompatible, typeof(OpenAICompatibleProvider))]
    [InlineData(LlmProviderType.AzureFoundry, typeof(AzureFoundryProvider))]
    public void Primary_IsCreatedAccordingToTheConfiguredType(LlmProviderType type, Type expected)
    {
        using var services = BuildServices(Options(type), _ => FakeHttpMessageHandler.ReturningCompletion("{}"));

        services.GetRequiredService<ILlmProvider>().ShouldBeOfType(expected);
    }

    [Fact]
    public void Fallback_WhenDisabled_IsNeverCreated()
    {
        var options = Options(LlmProviderType.OpenAICompatible);
        options.Fallback = new FallbackLlmProviderOptions
        {
            Enabled = false,
            Type = LlmProviderType.AzureFoundry,
            Endpoint = "https://respaldo.example.com/",
        };
        using var services = BuildServices(options, _ => FakeHttpMessageHandler.ReturningCompletion("{}"));

        var providers = services.GetRequiredService<LlmProviders>();

        providers.Fallback.ShouldBeNull();
        providers.All.ShouldHaveSingleItem().ShouldBeSameAs(providers.Primary);
    }

    [Fact]
    public async Task Fallback_WhenDisabledAndThePrimaryFails_NoRequestReachesTheFallback()
    {
        var options = Options(LlmProviderType.OpenAICompatible);
        options.Fallback = new FallbackLlmProviderOptions { Enabled = false };
        var fallbackHandler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""");
        using var services = BuildServices(options, name => name == LlmProviderFactory.FallbackHttpClient
            ? fallbackHandler
            : FakeHttpMessageHandler.Returning(HttpStatusCode.ServiceUnavailable, "{}"));

        var result = await services.GetRequiredService<ILlmProvider>()
            .CompleteStructuredAsync<PingReply>(Request(), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(LlmErrors.ProviderUnavailable);
        fallbackHandler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Fallback_WhenEnabled_IsCreatedAccordingToItsType()
    {
        var options = Options(LlmProviderType.OpenAICompatible);
        options.Fallback = Fallback();
        using var services = BuildServices(options, _ => FakeHttpMessageHandler.ReturningCompletion("{}"));

        var providers = services.GetRequiredService<LlmProviders>();

        providers.Fallback.ShouldBeOfType<AzureFoundryProvider>();
        providers.All.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Logs_NeverContainTheApiKeysNorTheContent_WhenCallsSucceedOrFail()
    {
        var options = Options(LlmProviderType.OpenAICompatible);
        options.Fallback = Fallback();
        var logs = new CapturingLoggerProvider();
        var responses = new Queue<(HttpStatusCode Status, string Body)>(
        [
            (HttpStatusCode.OK, FakeHttpMessageHandler.ChatCompletion("""{"ok":true}""", "modelo")),
            (HttpStatusCode.OK, FakeHttpMessageHandler.ChatCompletion("respuesta que no es JSON", "modelo")),
            (HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"Incorrect API key provided: " + LlmTestOptions.ApiKey + "\"}}"),
        ]);
        var handler = new FakeHttpMessageHandler((_, _) =>
        {
            var (status, body) = responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        });
        using var services = BuildServices(options, _ => handler, logs);

        foreach (var _ in Enumerable.Range(0, 3))
        {
            var providers = services.GetRequiredService<LlmProviders>();
            await providers.Primary.CompleteStructuredAsync<PingReply>(Request(), TestContext.Current.CancellationToken);
        }

        logs.Messages.ShouldContain(message => message.Contains("LLM call completed", StringComparison.Ordinal));
        logs.Messages.ShouldContain(message => message.Contains("does not match its schema", StringComparison.Ordinal));
        logs.Messages.ShouldContain(message => message.Contains("provider unavailable", StringComparison.Ordinal));
        logs.Messages.ShouldAllBe(message => !message.Contains(LlmTestOptions.ApiKey, StringComparison.Ordinal));
        logs.Messages.ShouldAllBe(message => !message.Contains("texto del documento", StringComparison.Ordinal));
        logs.Messages.ShouldAllBe(message => !message.Contains("respuesta que no es JSON", StringComparison.Ordinal));
    }

    private static LlmRequest Request() => new(LlmRole.Adapter, "sistema", "texto del documento", 100);

    private static LlmOptions Options(LlmProviderType type) => new()
    {
        Primary = LlmTestOptions.Provider(type, "https://principal.example.com/v1"),
    };

    private static FallbackLlmProviderOptions Fallback()
    {
        var source = LlmTestOptions.Provider(LlmProviderType.AzureFoundry, "https://respaldo.example.com/");
        return new FallbackLlmProviderOptions
        {
            Enabled = true,
            Type = source.Type,
            DisplayName = source.DisplayName,
            Endpoint = source.Endpoint,
            ApiKey = source.ApiKey,
            AdapterModel = source.AdapterModel,
            VerifierModel = source.VerifierModel,
        };
    }

    private static ServiceProvider BuildServices(
        LlmOptions options,
        Func<string, FakeHttpMessageHandler> handlerFor,
        CapturingLoggerProvider? logs = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            if (logs is not null)
            {
                logging.AddProvider(logs);
            }
        });
        services.AddOptions<LlmOptions>().Configure(configured =>
        {
            configured.Primary = options.Primary;
            configured.Fallback = options.Fallback;
        });
        services.AddOptions<LimitsOptions>();
        services.AddInfrastructure();
        foreach (var name in new[] { LlmProviderFactory.PrimaryHttpClient, LlmProviderFactory.FallbackHttpClient })
        {
            services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => handlerFor(name));
        }

        return services.BuildServiceProvider();
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IEnumerable<string> Messages => _messages;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                messages.Enqueue(state.ToString() ?? string.Empty);
                return null;
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue($"{formatter(state, exception)} {exception}");
        }
    }
}
