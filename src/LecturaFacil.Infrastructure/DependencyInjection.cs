// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Database";
    public const string DatabaseHealthCheckName = "database";

    /// <summary>Registra los adaptadores: base de datos y su comprobación de salud.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Las credenciales pueden llegar aparte (Database:Name, Database:Username, Database:Password)
        // para no tener que escaparlas a mano dentro de la cadena.
        var connectionString = DatabaseConnectionString.Build(
            configuration.GetConnectionString(ConnectionStringName),
            configuration["Database:Name"],
            configuration["Database:Username"],
            configuration["Database:Password"]);

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>(DatabaseHealthCheckName);

        return services;
    }
}
