// SPDX-License-Identifier: Apache-2.0
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Application;

public static class DependencyInjection
{
    /// <summary>Registra los casos de uso. Cada historia añade aquí los suyos.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
