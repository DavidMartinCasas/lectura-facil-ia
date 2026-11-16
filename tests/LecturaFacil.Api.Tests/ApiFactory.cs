// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Api.Tests.Security;
using LecturaFacil.Application.Llm;
using LecturaFacil.Infrastructure.Llm;
using LecturaFacil.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace LecturaFacil.Api.Tests;

/// <summary>API real contra un PostgreSQL en contenedor, en entorno de producción.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ThrowingPath = "/__tests/throw";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18.4-alpine").Build();

    protected virtual string Environment => "Production";

    protected virtual string ConnectionString => _database.GetConnectionString();

    /// <summary>
    /// Si es <c>true</c>, la sesión es la cookie real de Identity (tests de inicio de sesión). Si no, la da
    /// <see cref="TestAuthenticationHandler"/>.
    /// </summary>
    protected virtual bool UseRealAuthentication => false;

    /// <summary>Si es <c>false</c>, los documentos se quedan en el estado en que se guardan.</summary>
    protected virtual bool RunAdaptationWorker => false;

    /// <summary>El proveedor falso que usa la API. Por defecto responde con <c>adapter/ok</c>.</summary>
    /// <summary>Responde al adaptador y al verificador con sus grabaciones del ejemplo del IPREM.</summary>
    public FakeLlmProvider Llm { get; } = TestServices.ExampleLlm();

    public virtual async ValueTask InitializeAsync() => await _database.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        builder.UseSetting("Database:ConnectionString", ConnectionString);
        foreach (var (key, value) in TestConfiguration.ValidLlmSettings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ThrowingEndpointStartupFilter>());
        builder.ConfigureTestServices(services =>
        {
            services.UseFakeLlm(Llm);
            if (!UseRealAuthentication)
            {
                services.UseTestAuthentication();
            }

            if (!RunAdaptationWorker)
            {
                services.RemoveAdaptationWorker();
            }
        });
    }

    /// <summary>Añade, después de toda la tubería real, una ruta que lanza una excepción no controlada.</summary>
    private sealed class ThrowingEndpointStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map(ThrowingPath, branch => branch.Run(_ =>
                throw new InvalidOperationException("Detalle interno que no debe salir: /srv/secreto")));
        };
    }
}

/// <summary>Igual, pero sin base de datos disponible.</summary>
public sealed class ApiWithoutDatabaseFactory : ApiFactory
{
    protected override string ConnectionString => TestConfiguration.UnreachableDatabase;

    public override ValueTask InitializeAsync() => ValueTask.CompletedTask;
}

/// <summary>Entorno de desarrollo, para comprobar lo que solo se publica en él.</summary>
public sealed class DevelopmentApiFactory : ApiFactory
{
    protected override string Environment => "Development";
}

/// <summary>API con base de datos, pero con el proveedor principal caído según la comprobación de salud.</summary>
public sealed class ApiWithPrimaryLlmDownFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        var availability = new LlmAvailability(fallbackEnabled: false);
        availability.Update(new LlmAvailabilityStatus(PrimaryAvailable: false, FallbackAvailable: null));
        builder.ConfigureTestServices(services => services.AddSingleton(availability));
    }
}
