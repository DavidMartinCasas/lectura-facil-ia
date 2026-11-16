// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Users;
using LecturaFacil.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Infrastructure.Identity;

public static class IdentityRegistration
{
    /// <summary>
    /// Identity con sus tablas en <see cref="AppDbContext"/> y el puerto <see cref="IUserAccounts"/>. Las políticas
    /// (contraseña, bloqueo) las fija quien compone la aplicación en <paramref name="configure"/>; el inicio de sesión
    /// con cookie se añade en la API.
    /// </summary>
    public static IdentityBuilder AddUserAccounts(this IServiceCollection services, Action<IdentityOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IUserAccounts, IdentityUserAccounts>();
        return services.AddIdentityCore<ApplicationUser>(configure)
            .AddEntityFrameworkStores<AppDbContext>();
    }
}
