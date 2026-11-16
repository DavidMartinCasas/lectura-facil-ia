// SPDX-License-Identifier: Apache-2.0
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using LecturaFacil.Api.Tests.Documents;
using LecturaFacil.Application.Documents;
using LecturaFacil.Infrastructure.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace LecturaFacil.Api.Tests.Observability;

/// <summary>
/// La API sin base de datos (documentos en memoria), con el worker de adaptación y el proveedor falso, Sentry
/// habilitado con un transporte que guarda los eventos en lugar de enviarlos y un sink de Serilog en memoria.
/// </summary>
public sealed class ObservabilityApiFactory : WebApplicationFactory<Program>
{
    public const string ThrowingPath = "/__tests/observability/throw";

    /// <summary>Cabecera de los tests para simular la IP de quien llama (en TestServer no hay conexión real).</summary>
    public const string RemoteIpHeader = "X-Test-Remote-Ip";

    public const string BackendDsn = "https://clave-backend@sentry.example.com/1";
    public const string FrontendDsn = "https://clave-frontend@sentry.example.com/2";

    public RecordingDocumentRepository Repository { get; } = new();

    public InMemoryLogSink Logs { get; } = new();

    public RecordingSentryTransport Sentry { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("Database:ConnectionString", TestConfiguration.UnreachableDatabase);
        foreach (var (key, value) in TestConfiguration.ValidLlmSettings)
        {
            builder.UseSetting(key, value);
        }

        builder.UseSetting("Observability:SentryDsn", BackendDsn);
        builder.UseSetting("Observability:FrontendSentryDsn", FrontendDsn);
        builder.UseSetting("Observability:Environment", "pruebas");
        builder.UseSetting("Observability:Release", "abc1234");

        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, TestPipelineStartupFilter>());
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IDocumentRepository>(Repository);
            services.UseFakeLlm();
            services.AddSingleton<ILogEventSink>(Logs);
            services.Configure<SentryAspNetCoreOptions>(options =>
            {
                options.Transport = Sentry;
                options.FlushOnCompletedRequest = true;
            });
            services.AddHttpClient(SentryEnvelopeForwarder.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new FakeSentryServer(Sentry));
        });
    }

    /// <summary>Pone la IP simulada al principio y añade, al final, una ruta que lanza una excepción no controlada.</summary>
    private sealed class TestPipelineStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Headers.Remove(RemoteIpHeader, out var address))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(address.ToString());
                }

                return nextMiddleware(context);
            });
            next(app);
            app.Map(ThrowingPath, branch => branch.Run(_ =>
                throw new InvalidOperationException("Fallo de prueba en la API")));
        };
    }

    /// <summary>Hace de Sentry para el reenvío de los informes del navegador: guarda lo que le llega.</summary>
    private sealed class FakeSentryServer(RecordingSentryTransport sentry) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            sentry.Forwarded.Enqueue((request.RequestUri!, await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

/// <summary>Sink de Serilog que guarda cada evento ya escrito en JSON, como saldría por la consola.</summary>
public sealed class InMemoryLogSink : ILogEventSink
{
    private static readonly RenderedCompactJsonFormatter Formatter = new();

    private readonly ConcurrentQueue<string> _events = new();

    public IReadOnlyCollection<string> Events => _events;

    public void Emit(LogEvent logEvent)
    {
        using var writer = new StringWriter();
        Formatter.Format(logEvent, writer);
        _events.Enqueue(writer.ToString());
    }
}

/// <summary>Transporte de Sentry que guarda los sobres en lugar de enviarlos.</summary>
public sealed class RecordingSentryTransport : ITransport
{
    public ConcurrentQueue<string> Envelopes { get; } = new();

    /// <summary>Informes del navegador que la API ha reenviado: destino y cuerpo.</summary>
    public ConcurrentQueue<(Uri Target, string Body)> Forwarded { get; } = new();

    public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await envelope.SerializeAsync(stream, null, cancellationToken);
        Envelopes.Enqueue(Encoding.UTF8.GetString(stream.ToArray()));
    }
}
