// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Llm;
using LecturaFacil.Infrastructure.Documents;
using LecturaFacil.Infrastructure.Guidelines;
using LecturaFacil.Infrastructure.Llm;
using LecturaFacil.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseHealthCheckName = "database";

    /// <summary>
    /// Registra los adaptadores: base de datos y su comprobación de salud, documentos, pautas y proveedores de LLM.
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
        services.AddSingleton<IGuidelineChecker, GuidelineChecker>();

        return services.AddLlm();
    }

    private static IServiceCollection AddLlm(this IServiceCollection services)
    {
        services.AddSingleton<IPromptLoader, EmbeddedPromptLoader>();
        services.AddSingleton<LlmProviderFactory>();
        services.AddLlmHttpClient(LlmProviderFactory.PrimaryHttpClient);
        services.AddLlmHttpClient(LlmProviderFactory.FallbackHttpClient);

        // Transitorios: cada uso toma un HttpClient nuevo de IHttpClientFactory (rotación de conexiones y DNS).
        services.AddTransient(provider => LlmProviders.Create(
            provider.GetRequiredService<IOptions<LlmOptions>>(),
            provider.GetRequiredService<LlmProviderFactory>()));
        services.AddTransient(provider => provider.GetRequiredService<LlmProviders>().Primary);
        services.AddTransient<LlmDiagnostics>();
        return services;
    }

    /// <summary>Tiempo máximo explícito, sin seguir redirecciones y sin registrar el valor de ninguna cabecera.</summary>
    private static void AddLlmHttpClient(this IServiceCollection services, string name) =>
        services.AddHttpClient(name, (provider, client) =>
                client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<LimitsOptions>>().Value.LlmTimeoutSeconds))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders(_ => true);
}
