// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Llm;
using LecturaFacil.Application.Readability;
using LecturaFacil.Infrastructure.Adaptation;
using LecturaFacil.Infrastructure.Documents;
using LecturaFacil.Infrastructure.Guidelines;
using LecturaFacil.Infrastructure.Llm;
using LecturaFacil.Infrastructure.Persistence;
using LecturaFacil.Infrastructure.Readability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseHealthCheckName = "database";
    public const string LlmHealthCheckName = "llm-primary";

    /// <summary>Etiqueta de las comprobaciones del LLM: se exponen aparte de la salud de la API.</summary>
    public const string LlmHealthCheckTag = "llm";

    /// <summary>
    /// Registra los adaptadores: base de datos y su comprobación de salud, documentos, legibilidad y proveedores de LLM.
    /// Las opciones (<see cref="DatabaseOptions"/>, <see cref="LlmOptions"/>…) las registra y valida la composición de la API.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.BuildConnectionString()));
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>(DatabaseHealthCheckName);
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddSingleton<ITextExtractor, PdfPigTextExtractor>();
        services.AddSingleton<IReadabilityCalculator, SzigrisztPazosReadabilityCalculator>();
        services.AddSingleton<IGuidelineChecker, GuidelineChecker>();

        return services.AddAdaptation().AddLlm();
    }

    /// <summary>Cola en memoria y proceso en segundo plano que adapta los documentos.</summary>
    private static IServiceCollection AddAdaptation(this IServiceCollection services)
    {
        services.AddSingleton<ChannelAdaptationQueue>();
        services.AddSingleton<IAdaptationQueue>(provider => provider.GetRequiredService<ChannelAdaptationQueue>());
        services.AddHostedService<AdaptationWorker>();
        return services;
    }

    private static IServiceCollection AddLlm(this IServiceCollection services)
    {
        services.AddSingleton<IPromptLoader, EmbeddedPromptLoader>();
        services.AddSingleton<LlmProviderFactory>();
        services.AddLlmHttpClient(LlmProviderFactory.PrimaryHttpClient)
            .AddResilienceHandler(LlmResiliencePipeline.Name, LlmResiliencePipeline.Configure);
        services.AddLlmHttpClient(LlmProviderFactory.FallbackHttpClient);

        // Transitorios: cada uso toma un HttpClient nuevo de IHttpClientFactory (rotación de conexiones y DNS).
        services.AddTransient(provider => LlmProviders.Create(
            provider.GetRequiredService<IOptions<LlmOptions>>(),
            provider.GetRequiredService<LlmProviderFactory>()));
        services.AddTransient<LlmDiagnostics>();

        // Los casos de uso solo ven el decorador con la política de respaldo.
        services.AddSingleton(provider => new LlmAvailability(FallbackEnabled(provider)));
        services.AddSingleton<ILlmAvailability>(provider => provider.GetRequiredService<LlmAvailability>());
        services.AddTransient<ILlmProvider>(provider =>
        {
            var providers = provider.GetRequiredService<LlmProviders>();
            return new ResilientLlmProvider(
                providers.Primary,
                providers.Fallback,
                FallbackEnabled(provider),
                provider.GetRequiredService<ILlmAvailability>());
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService(provider => ActivatorUtilities.CreateInstance<LlmHealthMonitor>(
            provider,
            new Func<LlmProviders>(provider.GetRequiredService<LlmProviders>)));
        services.AddHealthChecks().AddCheck<LlmHealthCheck>(LlmHealthCheckName, HealthStatus.Degraded, [LlmHealthCheckTag]);
        return services;
    }

    private static bool FallbackEnabled(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<LlmOptions>>().Value.Fallback.Enabled;

    /// <summary>Tiempo máximo explícito, sin seguir redirecciones y sin registrar el valor de ninguna cabecera.</summary>
    private static IHttpClientBuilder AddLlmHttpClient(this IServiceCollection services, string name) =>
        services.AddHttpClient(name, (provider, client) =>
                client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<LimitsOptions>>().Value.LlmTimeoutSeconds))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders(_ => true);
}
