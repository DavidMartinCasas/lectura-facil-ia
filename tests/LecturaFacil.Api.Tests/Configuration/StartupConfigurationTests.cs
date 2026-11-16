// SPDX-License-Identifier: Apache-2.0
using System.Collections.Concurrent;
using System.Net;
using LecturaFacil.Api.Configuration;
using LecturaFacil.Application.Common;
using LecturaFacil.Infrastructure.Llm;
using LecturaFacil.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Api.Tests.Configuration;

/// <summary>Arranque de la API con distintas configuraciones. No necesita base de datos: nada la usa al arrancar.</summary>
public sealed class StartupConfigurationTests
{
    private const string SecretValue = "secreto-secreto-secreto";

    [Fact]
    public async Task Start_WhenConfigurationIsComplete_StartsAndServesRequests()
    {
        await using var factory = new ConfiguredApiFactory(CompleteSettings());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/no-existe", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Start_WithTheValuesOfEnvExample_Starts()
    {
        var settings = EnvExample.ReadApiSettings();
        settings["Database:ConnectionString"] = TestConfiguration.UnreachableDatabase;
        await using var factory = new ConfiguredApiFactory(settings);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/no-existe", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        settings.Keys.ShouldContain("Llm:Primary:ApiKey");
    }

    [Theory]
    [InlineData("Llm:Primary:Type", "TipoDesconocido")]
    [InlineData("Limits:MaxCharacters", "muchos")]
    public async Task Start_WhenValueCannotBeConverted_DoesNotStartAndLogsTheOption(string key, string value)
    {
        var settings = CompleteSettings();
        settings[key] = value;
        var logs = new CapturingLoggerProvider();
        await using var factory = new ConfiguredApiFactory(settings, logs);

        var exception = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        exception.Message.ShouldContain(key);
        exception.Message.ShouldNotContain(value);
        logs.Messages.ShouldContain(message => message.Contains(key, StringComparison.Ordinal));
        logs.Messages.ShouldAllBe(message => !message.Contains(value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Start_WhenNothingOptionalIsConfigured_UsesSafeDefaults()
    {
        await using var factory = new ConfiguredApiFactory(CompleteSettings());
        using var client = factory.CreateClient();
        var services = factory.Services;

        services.GetRequiredService<IOptions<LlmOptions>>().Value.Fallback.Enabled.ShouldBeFalse();
        services.GetRequiredService<IOptions<UiOptions>>().Value.Notice.ShouldBeEmpty();
        services.GetRequiredService<IOptions<LimitsOptions>>().Value.MaxWordsPerSentence.ShouldBe(15);
        services.GetRequiredService<IOptions<DatabaseOptions>>().Value.ApplyMigrationsOnStartup.ShouldBeFalse();
        services.GetRequiredService<IOptions<LlmResilienceOptions>>().Value.MaxRetryAttempts.ShouldBe(1);
    }

    [Theory]
    [InlineData("Llm:Primary:ApiKey")]
    [InlineData("Llm:Primary:Endpoint")]
    [InlineData("Llm:Primary:Type")]
    [InlineData("Database:ConnectionString")]
    public async Task Start_WhenRequiredOptionIsMissing_DoesNotStartAndNamesTheOption(string missingKey)
    {
        var settings = CompleteSettings();
        settings.Remove(missingKey);
        await using var factory = new ConfiguredApiFactory(settings);

        var exception = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        exception.Message.ShouldContain(missingKey);
    }

    [Fact]
    public async Task Start_WhenFallbackIsEnabledWithoutItsConfiguration_DoesNotStart()
    {
        var settings = CompleteSettings();
        settings["Llm:Fallback:Enabled"] = "true";
        await using var factory = new ConfiguredApiFactory(settings);

        var exception = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        exception.Message.ShouldContain("Llm:Fallback:ApiKey");
    }

    [Theory]
    [InlineData("Limits:MaxWordsPerSentence", "0")]
    [InlineData("Llm:Primary:Type", "999")]
    [InlineData("Ui:Notice", "x")]
    [InlineData("Llm:Resilience:BreakDurationSeconds", "0")]
    public async Task Start_WhenOptionIsOutOfRange_DoesNotStart(string key, string value)
    {
        var settings = CompleteSettings();
        settings[key] = key == "Ui:Notice" ? new string('x', UiOptions.MaxNoticeLength + 1) : value;
        await using var factory = new ConfiguredApiFactory(settings);

        var exception = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        exception.Message.ShouldContain(key);
    }

    [Fact]
    public async Task Start_WhenSeveralOptionsAreInvalid_ReportsAllOfThem()
    {
        var settings = CompleteSettings();
        settings.Remove("Llm:Primary:ApiKey");
        settings.Remove("Database:ConnectionString");
        await using var factory = new ConfiguredApiFactory(settings);

        var exception = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        exception.Message.ShouldContain("Llm:Primary:ApiKey");
        exception.Message.ShouldContain("Database:ConnectionString");
    }

    [Fact]
    public async Task Start_WhenValidationFails_LogsWhichOptionFails()
    {
        var settings = CompleteSettings();
        settings.Remove("Llm:Primary:ApiKey");
        var logs = new CapturingLoggerProvider();
        await using var factory = new ConfiguredApiFactory(settings, logs);

        Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        logs.Messages.ShouldContain(message => message.Contains("Llm:Primary:ApiKey", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Start_WhenValidationFails_NeitherTheErrorNorTheLogsContainSecrets()
    {
        var settings = CompleteSettings();
        settings["Llm:Primary:ApiKey"] = SecretValue;
        settings["Llm:Primary:Endpoint"] = "http://proveedor-publico.example.com/v1";
        settings["Llm:Fallback:Enabled"] = "true";
        settings["Llm:Fallback:ApiKey"] = SecretValue;
        settings["Database:Password"] = SecretValue;
        settings["Database:ConnectionString"] = $"Database=sin-host;Password={SecretValue}";
        var logs = new CapturingLoggerProvider();
        await using var factory = new ConfiguredApiFactory(settings, logs);

        var exception = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        exception.Message.ShouldContain("Llm:Primary:Endpoint");
        exception.Message.ShouldContain("Database:ConnectionString");
        exception.Message.ShouldNotContain(SecretValue);
        logs.Messages.ShouldNotBeEmpty();
        logs.Messages.ShouldAllBe(message => !message.Contains(SecretValue, StringComparison.Ordinal));
    }

    private static Dictionary<string, string?> CompleteSettings()
    {
        var settings = TestConfiguration.ValidLlmSettings.ToDictionary(pair => pair.Key, string? (pair) => pair.Value);
        settings["Database:ConnectionString"] = TestConfiguration.UnreachableDatabase;
        return settings;
    }

    private sealed class ConfiguredApiFactory(
        IReadOnlyDictionary<string, string?> settings,
        ILoggerProvider? loggerProvider = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            // Ni el .env.example ni ninguna otra configuración de estos tests puede llegar a llamar a un proveedor.
            builder.UseSetting(TestConfiguration.HealthCheckEnabledKey, "false");

            if (loggerProvider is not null)
            {
                builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider));
            }
        }
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
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue($"{formatter(state, exception)} {exception}");
        }
    }
}
