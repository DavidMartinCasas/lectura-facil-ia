// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics.Metrics;
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Llm;
using Microsoft.Extensions.Logging;

namespace LecturaFacil.Infrastructure.Observability;

/// <summary>
/// El único sitio que define los campos del log y las métricas de una adaptación (US-17). Solo usa lo que trae
/// <see cref="AdaptationOutcome"/>: identificador, proveedor, modelo, tokens, duración y resultado; nunca el texto.
/// </summary>
public sealed partial class AdaptationTelemetry : IAdaptationTelemetry
{
    public const string MeterName = "LecturaFacil.Adaptation";

    /// <summary>Etiqueta del proveedor que respondió cuando ningún modelo dio una respuesta válida.</summary>
    public const string NoProvider = "none";

    private readonly ILogger<AdaptationTelemetry> _logger;
    private readonly Counter<long> _adaptations;
    private readonly Counter<long> _errors;
    private readonly Counter<long> _tokens;
    private readonly Histogram<double> _duration;

    public AdaptationTelemetry(IMeterFactory meterFactory, ILogger<AdaptationTelemetry> logger)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _logger = logger;
        var meter = meterFactory.Create(MeterName);
        _adaptations = meter.CreateCounter<long>("lecturafacil.adaptations", description: "Adaptaciones terminadas, por proveedor y resultado.");
        _errors = meter.CreateCounter<long>("lecturafacil.adaptation.errors", description: "Adaptaciones fallidas, por código de error.");
        _tokens = meter.CreateCounter<long>("lecturafacil.adaptation.tokens", description: "Tokens de las respuestas del adaptador, por proveedor y sentido.");
        _duration = meter.CreateHistogram<double>("lecturafacil.adaptation.duration", unit: "s", description: "Duración de cada adaptación.");
    }

    public void Record(AdaptationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var role = RoleLabel(outcome.ProviderRole);
        var result = outcome.IsSuccess ? "success" : "failure";
        var durationMs = (long)outcome.Duration.TotalMilliseconds;
        if (outcome.IsSuccess)
        {
            LogAdapted(outcome.DocumentId.Value, role, outcome.Provider, outcome.Model, outcome.InputTokens, outcome.OutputTokens, durationMs, outcome.Result);
        }
        else
        {
            LogNotAdapted(outcome.DocumentId.Value, role, outcome.Provider, outcome.Model, outcome.InputTokens, outcome.OutputTokens, durationMs, outcome.Result);
            _errors.Add(1, new KeyValuePair<string, object?>("error", outcome.Result));
        }

        var roleTag = new KeyValuePair<string, object?>("provider_role", role);
        _adaptations.Add(1, roleTag, new KeyValuePair<string, object?>("result", result));
        _duration.Record(outcome.Duration.TotalSeconds, roleTag, new KeyValuePair<string, object?>("result", result));
        _tokens.Add(outcome.InputTokens, roleTag, new KeyValuePair<string, object?>("direction", "input"));
        _tokens.Add(outcome.OutputTokens, roleTag, new KeyValuePair<string, object?>("direction", "output"));
    }

    private static string RoleLabel(LlmProviderRole? role) => role switch
    {
        LlmProviderRole.Primary => "primary",
        LlmProviderRole.Fallback => "fallback",
        _ => NoProvider,
    };

    [LoggerMessage(
        EventName = "AdaptationCompleted",
        Level = LogLevel.Information,
        Message = "Document {DocumentId} adapted by {ProviderRole} provider {Provider} ({Model}): {InputTokens} input and {OutputTokens} output tokens in {DurationMs} ms. Result: {Result}.")]
    private partial void LogAdapted(Guid documentId, string providerRole, string? provider, string? model, long inputTokens, long outputTokens, long durationMs, string result);

    [LoggerMessage(
        EventName = "AdaptationFailed",
        Level = LogLevel.Warning,
        Message = "Document {DocumentId} not adapted; last {ProviderRole} provider {Provider} ({Model}): {InputTokens} input and {OutputTokens} output tokens in {DurationMs} ms. Result: {Result}.")]
    private partial void LogNotAdapted(Guid documentId, string providerRole, string? provider, string? model, long inputTokens, long outputTokens, long durationMs, string result);
}
