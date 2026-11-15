// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseHealthCheckName = "database";

    /// <summary>
    /// Registra los adaptadores: base de datos y su comprobación de salud.
    /// Las opciones (<see cref="DatabaseOptions"/>…) las registra y valida la composición de la API.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.BuildConnectionString()));
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>(DatabaseHealthCheckName);

        return services;
    }
}
