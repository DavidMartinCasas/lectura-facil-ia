// SPDX-License-Identifier: Apache-2.0
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace LecturaFacil.Api.Tests;

/// <summary>API real contra un PostgreSQL en contenedor, en entorno de producción.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ThrowingPath = "/__tests/throw";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18-alpine").Build();

    protected virtual string Environment => "Production";

    protected virtual string ConnectionString => _database.GetConnectionString();

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
        builder.UseSetting("ConnectionStrings:Database", ConnectionString);
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ThrowingEndpointStartupFilter>());
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
    protected override string ConnectionString => "Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=2";

    public override ValueTask InitializeAsync() => ValueTask.CompletedTask;
}

/// <summary>Entorno de desarrollo, para comprobar lo que solo se publica en él.</summary>
public sealed class DevelopmentApiFactory : ApiFactory
{
    protected override string Environment => "Development";
}
