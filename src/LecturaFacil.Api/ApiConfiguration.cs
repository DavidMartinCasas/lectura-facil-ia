// SPDX-License-Identifier: Apache-2.0
using FluentValidation;
using LecturaFacil.Api.Documents;
using LecturaFacil.Api.Llm;
using LecturaFacil.Api.Security;
using LecturaFacil.Application.Users;

namespace LecturaFacil.Api;

internal static class ApiConfiguration
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddOpenApi();
        services.AddRequestLimits();
        services.AddScoped<ICurrentUser, FixedCurrentUser>();
        services.AddSingleton<IValidator<SubmitTextRequest>, SubmitTextRequestValidator>();
        services.AddSingleton<IValidator<SubmitPdfForm>, SubmitPdfFormValidator>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddLlmDiagnosticsRateLimit();
            options.AddDocumentSubmitRateLimit();
        });
        return services;
    }

    public static WebApplication UseApi(this WebApplication app)
    {
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseRateLimiter();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        return app;
    }
}
