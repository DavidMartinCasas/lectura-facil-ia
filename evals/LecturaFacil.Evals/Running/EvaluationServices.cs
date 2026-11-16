// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application;
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Llm;
using LecturaFacil.Application.Readability;
using LecturaFacil.Application.Users;
using LecturaFacil.Application.Verification;
using LecturaFacil.Infrastructure.Documents;
using LecturaFacil.Infrastructure.Guidelines;
using LecturaFacil.Infrastructure.Llm;
using LecturaFacil.Infrastructure.Readability;
using LecturaFacil.Infrastructure.Verification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Evals.Running;

/// <summary>
/// Composición de la evaluación: los casos de uso de <see cref="DependencyInjection.AddApplication"/> y los mismos
/// adaptadores que la API (pautas, legibilidad, prompts, verificador), con un repositorio en memoria y sin cola,
/// telemetría ni base de datos. Cada perfil tiene su propia composición con su proveedor.
/// </summary>
internal static class EvaluationServices
{
    public static ServiceProvider Build(LimitsOptions limits, Func<IServiceProvider, ILlmProvider> llmProvider)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(llmProvider);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton<IOptions<LimitsOptions>>(Options.Create(limits));
        services.AddSingleton<IPromptLoader, EmbeddedPromptLoader>();
        services.AddSingleton<IGuidelineChecker, GuidelineChecker>();
        services.AddSingleton<IReadabilityCalculator, SzigrisztPazosReadabilityCalculator>();
        services.AddSingleton<ITextExtractor, PdfPigTextExtractor>();
        services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();
        services.AddSingleton<ICurrentUser, EvaluationUser>();
        services.AddSingleton<IAdaptationQueue, NoAdaptationQueue>();
        services.AddSingleton<IAdaptationTelemetry, NoAdaptationTelemetry>();

        // Sin respaldo: cada perfil se evalúa solo con su proveedor, para que los resultados sean de ese modelo. Con la
        // misma política de reintentos que el principal en producción: un fallo transitorio no cuenta contra el modelo.
        services.AddHttpClient(LlmProviderFactory.PrimaryHttpClient, client => client.Timeout = TimeSpan.FromSeconds(limits.LlmTimeoutSeconds))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders(_ => true)
            .AddResilienceHandler(LlmResiliencePipeline.Name, LlmResiliencePipeline.Configure);
        services.AddSingleton<LlmProviderFactory>();
        services.AddSingleton(provider => new MeteredLlmProvider(llmProvider(provider)));
        services.AddSingleton<ILlmProvider>(provider => provider.GetRequiredService<MeteredLlmProvider>());
        services.AddTransient<IMeaningVerifier, LlmMeaningVerifier>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
