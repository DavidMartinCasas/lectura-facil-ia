// SPDX-License-Identifier: Apache-2.0
using FluentValidation;
using LecturaFacil.Api.Auth;
using LecturaFacil.Api.Documents;
using LecturaFacil.Api.Llm;
using LecturaFacil.Api.Observability;
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
        services.AddCookieAuthentication();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddSingleton<IValidator<SubmitTextRequest>, SubmitTextRequestValidator>();
        services.AddSingleton<IValidator<SubmitPdfForm>, SubmitPdfFormValidator>();
        services.AddSingleton<IValidator<ManualEditRequest>, ManualEditRequestValidator>();
        services.AddSingleton<IValidator<AcknowledgeKeyFactRequest>, AcknowledgeKeyFactRequestValidator>();
        services.AddSingleton<IValidator<RecordValidationRequest>, RecordValidationRequestValidator>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = RateLimitPartitions.AddRetryAfter;
            options.AddLlmDiagnosticsRateLimit();
            options.AddAdaptationsRateLimit();
            options.AddExportsRateLimit();
            options.AddMonitoringTunnelRateLimit();
            options.AddLoginRateLimit();
        });
        return services;
    }

    public static WebApplication UseApi(this WebApplication app)
    {
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAntiforgeryTokenCookie();
        app.UseAuthorization();
        app.UseAntiforgeryValidation();

        // Después de la autorización: una petición sin sesión (401) no gasta el cupo de los usuarios con sesión.
        app.UseRateLimiter();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        return app;
    }
}
