// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Resiliencia del proveedor principal: reintento corto, circuit breaker y comprobación de salud periódica.
/// El tiempo máximo de cada llamada es <c>Limits:LlmTimeoutSeconds</c> e incluye los reintentos.
/// </summary>
public sealed class LlmResilienceOptions
{
    public const string SectionName = "Llm:Resilience";
    public const int MinRetryDelayMilliseconds = 100;

    /// <summary>Reintentos ante un error transitorio (5xx, 408, 429 o sin conexión). 0 = sin reintentos.</summary>
    [Range(0, 3, ErrorMessage = "Llm:Resilience:MaxRetryAttempts debe estar entre {1} y {2}.")]
    public int MaxRetryAttempts { get; set; } = 1;

    [Range(MinRetryDelayMilliseconds, 10_000, ErrorMessage = "Llm:Resilience:RetryDelayMilliseconds debe estar entre {1} y {2}.")]
    public int RetryDelayMilliseconds { get; set; } = 500;

    /// <summary>Proporción de fallos, dentro de la ventana de muestreo, que abre el circuito.</summary>
    [Range(0.1, 1.0, ErrorMessage = "Llm:Resilience:FailureRatio debe estar entre {1} y {2}.")]
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>Intentos mínimos en la ventana de muestreo para que el circuito pueda abrirse.</summary>
    [Range(2, 100, ErrorMessage = "Llm:Resilience:MinimumThroughput debe estar entre {1} y {2}.")]
    public int MinimumThroughput { get; set; } = 6;

    [Range(1, 600, ErrorMessage = "Llm:Resilience:SamplingDurationSeconds debe estar entre {1} y {2}.")]
    public int SamplingDurationSeconds { get; set; } = 60;

    /// <summary>Tiempo que el circuito queda abierto antes de dejar pasar una petición de prueba.</summary>
    [Range(1, 3_600, ErrorMessage = "Llm:Resilience:BreakDurationSeconds debe estar entre {1} y {2}.")]
    public int BreakDurationSeconds { get; set; } = 30;

    /// <summary>Comprobación periódica de los proveedores con una petición mínima, sin ningún documento.</summary>
    public bool HealthCheckEnabled { get; set; } = true;

    [Range(5, 3_600, ErrorMessage = "Llm:Resilience:HealthCheckIntervalSeconds debe estar entre {1} y {2}.")]
    public int HealthCheckIntervalSeconds { get; set; } = 30;
}
