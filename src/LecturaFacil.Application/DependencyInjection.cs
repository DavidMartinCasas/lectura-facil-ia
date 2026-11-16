// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Readability;
using LecturaFacil.Application.Verification;
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
        services.AddScoped<CompareReadability>();
        services.AddScoped<GetDocument>();
        services.AddScoped<ListDocuments>();
        services.AddScoped<WatchDocument>();
        services.AddScoped<ReviewAdaptation>();
        services.AddScoped<VerifyMeaning>();
        services.AddScoped<AdaptDocument>();
        services.AddScoped<RequeuePendingAdaptations>();
        return services;
    }
}
