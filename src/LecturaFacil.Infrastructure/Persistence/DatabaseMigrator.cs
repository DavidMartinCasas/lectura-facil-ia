// SPDX-License-Identifier: Apache-2.0
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    /// <summary>Aplica las migraciones pendientes. Se usa al arrancar mientras no haya un paso de despliegue propio.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
