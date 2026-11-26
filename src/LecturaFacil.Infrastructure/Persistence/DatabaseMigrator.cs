// SPDX-License-Identifier: Apache-2.0
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    /// <summary>Argumento de la API que aplica las migraciones y termina, sin atender peticiones.</summary>
    public const string MigrateArgument = "--migrate";

    /// <summary>
    /// Aplica las migraciones pendientes: en el despliegue, con <see cref="MigrateArgument"/>; en desarrollo, al arrancar
    /// si <see cref="DatabaseOptions.ApplyMigrationsOnStartup"/> está activado.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
