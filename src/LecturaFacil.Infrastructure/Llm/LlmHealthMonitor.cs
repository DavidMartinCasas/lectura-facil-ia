// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Llm;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Comprueba cada <see cref="LlmResilienceOptions.HealthCheckIntervalSeconds"/> segundos si el principal (y el respaldo,
/// solo si está habilitado) responde, con la petición mínima del diagnóstico: nunca envía ningún documento.
/// </summary>
public sealed partial class LlmHealthMonitor(
    Func<LlmProviders> providers,
    IPromptLoader prompts,
    LlmAvailability availability,
    IOptions<LlmResilienceOptions> options,
    TimeProvider timeProvider,
    ILogger<LlmHealthMonitor> logger) : BackgroundService
{
    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        var systemPrompt = prompts.Load(LlmDiagnostics.PingPrompt).Text;
        var current = providers();

        var primary = await RespondsAsync(current.Primary, systemPrompt, cancellationToken);
        bool? fallback = current.Fallback is null
            ? null
            : await RespondsAsync(current.Fallback, systemPrompt, cancellationToken);

        var previous = availability.Update(new LlmAvailabilityStatus(primary, fallback));
        LogIfChanged(current.Primary.Name, previous.PrimaryAvailable, primary);
        if (current.Fallback is not null && fallback is { } fallbackAvailable)
        {
            LogIfChanged(current.Fallback.Name, previous.FallbackAvailable, fallbackAvailable);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.HealthCheckEnabled)
        {
            return;
        }

        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.HealthCheckIntervalSeconds), timeProvider);
        do
        {
            await ProbeSafelyAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Un fallo inesperado de la comprobación no puede parar la API: se registra solo su tipo (el mensaje podría
    /// contener datos) y se conserva el último estado conocido hasta la siguiente comprobación.
    /// </summary>
    private async Task ProbeSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ProbeAsync(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogProbeFailed(exception.GetType().Name);
        }
    }

    /// <summary>Una respuesta que no cumple el esquema también cuenta: el proveedor está ahí y responde.</summary>
    private static async Task<bool> RespondsAsync(ILlmProvider provider, string systemPrompt, CancellationToken cancellationToken)
    {
        var result = await provider.CompleteStructuredAsync<PingReply>(
            LlmDiagnostics.PingRequest(systemPrompt, LlmRole.Adapter),
            cancellationToken);
        return result.Error != LlmErrors.ProviderUnavailable;
    }

    private void LogIfChanged(string provider, bool? previous, bool available)
    {
        if (previous != available)
        {
            LogAvailabilityChanged(provider, available);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM provider availability changed. Provider: {Provider}; available: {Available}")]
    private partial void LogAvailabilityChanged(string provider, bool available);

    [LoggerMessage(Level = LogLevel.Error, Message = "LLM health check failed unexpectedly; keeping the last known state. Reason: {Reason}")]
    private partial void LogProbeFailed(string reason);
}
