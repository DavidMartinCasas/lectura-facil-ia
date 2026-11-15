// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Api.Security;

namespace LecturaFacil.Api;

internal static class ApiConfiguration
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddOpenApi();
        return services;
    }

    public static WebApplication UseApi(this WebApplication app)
    {
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        return app;
    }
}
