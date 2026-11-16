// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LecturaFacil.Application;

public static class DependencyInjection
{
    /// <summary>Registra los casos de uso. Cada historia añade aquí los suyos.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<SubmitDocument>();
        services.AddScoped<GetDocument>();
        services.AddScoped<WatchDocument>();
        services.AddScoped<AdaptDocument>();
        services.AddScoped<RequeuePendingAdaptations>();
        return services;
    }
}
