// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Export;
using LecturaFacil.Application.Readability;
using LecturaFacil.Application.Review;
using LecturaFacil.Application.Users;
using LecturaFacil.Application.Validation;
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
        services.AddScoped<DescribeDocument>();
        services.AddScoped<GetDocument>();
        services.AddScoped<ListDocuments>();
        services.AddScoped<RetryAdaptation>();
        services.AddScoped<WatchDocument>();
        services.AddScoped<ReviewAdaptation>();
        services.AddScoped<VerifyMeaning>();
        services.AddScoped<AdaptDocument>();
        services.AddScoped<RequeuePendingAdaptations>();
        services.AddScoped<SaveManualEdit>();
        services.AddScoped<AcknowledgeKeyFact>();
        services.AddScoped<AcknowledgeAllKeyFacts>();
        services.AddScoped<ValidationStep>();
        services.AddScoped<SendToValidation>();
        services.AddScoped<RecordValidation>();
        services.AddScoped<WithdrawFromValidation>();
        services.AddScoped<PublishDocument>();
        services.AddScoped<ExportDocument>();
        services.AddScoped<SeedTestUser>();
        return services;
    }
}
