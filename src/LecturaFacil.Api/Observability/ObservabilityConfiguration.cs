// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Observability;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Compact;

namespace LecturaFacil.Api.Observability;

/// <summary>
/// Logs en JSON con Serilog y errores en Sentry (US-17). El nivel de log se configura en la sección <c>Serilog</c>
/// (por ejemplo <c>Serilog__MinimumLevel__Default=Warning</c>). Cada evento lleva el <c>TraceId</c> de la petición y,
/// en el worker de adaptación, el <c>DocumentId</c>. Sentry queda deshabilitado si no hay DSN.
/// </summary>
internal static class ObservabilityConfiguration
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.WebHost.UseSentry((context, sentry) => ConfigureSentry(sentry, Options(context.Configuration)));

        // Serilog es un proveedor más: los logs llegan también al de Sentry (errores como eventos y el resto como
        // migas). El nivel lo decide Serilog (sección "Serilog"), por eso el filtro general deja pasar todo.
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Services.AddSingleton<ILoggerProvider>(services => new SerilogLoggerProvider(
            new LoggerConfiguration()
                .ReadFrom.Configuration(services.GetRequiredService<IConfiguration>())
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                .CreateLogger(),
            dispose: true));

        builder.Services.AddSingleton(provider => AppVersion.From(Options(provider.GetRequiredService<IConfiguration>())));
        return builder;
    }

    public static void ConfigureSentry(SentryAspNetCoreOptions sentry, ObservabilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(sentry);
        ArgumentNullException.ThrowIfNull(options);

        // Cadena vacía: SDK deshabilitado. Nunca se toma el DSN de otra sección ni de SENTRY_DSN.
        sentry.Dsn = options.SentryDsn;
        sentry.Environment = options.Environment;
        sentry.Release = AppVersion.From(options).Value;
        sentry.SendDefaultPii = false;
        sentry.MaxRequestBodySize = RequestSize.None;
        sentry.IncludeActivityData = false;
        sentry.TracesSampleRate = 0;
        sentry.SetBeforeSend((sentryEvent, _) => SentryEventScrubber.Scrub(sentryEvent));
        sentry.SetBeforeBreadcrumb((breadcrumb, _) => SentryEventScrubber.Scrub(breadcrumb));
    }

    /// <summary>
    /// Las opciones tal como están en la configuración. Si no son válidas, la validación al arrancar para la API
    /// (<c>ValidateOptionsOrStop</c>): aquí no se valida para no duplicar mensajes.
    /// </summary>
    private static ObservabilityOptions Options(IConfiguration configuration) =>
        configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ?? new ObservabilityOptions();
}
