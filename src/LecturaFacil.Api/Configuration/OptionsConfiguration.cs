// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Api.Security;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Users;
using LecturaFacil.Infrastructure.Export;
using LecturaFacil.Infrastructure.Llm;
using LecturaFacil.Infrastructure.Persistence;

namespace LecturaFacil.Api.Configuration;

/// <summary>
/// Registra todas las opciones tipadas. Se validan al arrancar: si falta algo o no es válido, la API no arranca.
/// </summary>
internal static class OptionsConfiguration
{
    public static IServiceCollection AddValidatedOptions(this IServiceCollection services)
    {
        services.AddValidated<DatabaseOptions>(DatabaseOptions.SectionName);
        services.AddValidated<LlmOptions>(LlmOptions.SectionName);
        services.AddValidated<LlmResilienceOptions>(LlmResilienceOptions.SectionName);
        services.AddValidated<LimitsOptions>(LimitsOptions.SectionName);
        services.AddValidated<UiOptions>(UiOptions.SectionName);
        services.AddValidated<ExportOptions>(ExportOptions.SectionName);
        services.AddValidated<AuthOptions>(AuthOptions.SectionName);
        services.AddValidated<TestUserOptions>(TestUserOptions.SectionName);
        return services;
    }

    private static void AddValidated<TOptions>(this IServiceCollection services, string sectionName)
        where TOptions : class =>
        services.AddOptions<TOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
}
