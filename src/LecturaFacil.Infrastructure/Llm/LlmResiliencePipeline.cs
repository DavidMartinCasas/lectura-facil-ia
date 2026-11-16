// SPDX-License-Identifier: Apache-2.0
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Tubería del cliente HTTP del principal: reintento corto y circuit breaker. Con el circuito abierto, las llamadas
/// fallan al momento (el proveedor devuelve "no disponible") hasta que, pasado el tiempo de corte, una petición de
/// prueba comprueba si el principal ha vuelto.
/// </summary>
internal static class LlmResiliencePipeline
{
    public const string Name = "llm-primary";

    public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder, ResilienceHandlerContext context)
    {
        var options = context.ServiceProvider.GetRequiredService<IOptions<LlmResilienceOptions>>().Value;

        if (options.MaxRetryAttempts > 0)
        {
            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds),
                BackoffType = DelayBackoffType.Constant,
                UseJitter = true,

                // Un Retry-After largo no puede alargar la espera: el reintento es siempre corto.
                ShouldRetryAfterHeader = false,
            });
        }

        builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = options.FailureRatio,
            MinimumThroughput = options.MinimumThroughput,
            SamplingDuration = TimeSpan.FromSeconds(options.SamplingDurationSeconds),
            BreakDuration = TimeSpan.FromSeconds(options.BreakDurationSeconds),
        });
    }
}
