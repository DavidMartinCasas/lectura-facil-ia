// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Api.Auth;
using LecturaFacil.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Api.Security;

/// <summary>
/// Sesión con cookie del propio backend (estilo BFF) sobre ASP.NET Core Identity (US-15). Nada de tokens en el
/// navegador: la cookie es HttpOnly, Secure y SameSite=Strict. Toda ruta exige sesión salvo las marcadas como anónimas.
/// </summary>
internal static class AuthenticationConfiguration
{
    public const string SessionCookieName = "__Host-lf-session";
    /// <summary>Sin prefijo "__Host-": ese prefijo exige Secure y detrás de un proxy HTTP el navegador la descartaría.</summary>
    public const string AntiforgeryCookieName = "lf-antiforgery";

    public static IServiceCollection AddCookieAuthentication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddUserAccounts(_ => { })
            .AddSignInManager();
        services.AddOptions<IdentityOptions>().Configure<IOptions<AuthOptions>>((identity, auth) => ConfigureIdentity(identity, auth.Value));

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<AuthOptions>>((cookie, auth) => ConfigureSessionCookie(cookie, auth.Value));

        // Comprueba el sello de seguridad en cada petición: al cerrar sesión se cambia y la cookie deja de valer en el servidor.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryMiddleware.HeaderName;
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;

            // Detrás de un proxy que termina TLS la API recibe HTTP: con "Always" el antiforgery lanzaría una excepción.
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.SuppressXFrameOptionsHeader = true;
        });

        return services;
    }

    /// <summary>
    /// Límite de intentos de inicio de sesión por dirección IP del cliente (OWASP API2 y API4). Detrás del nginx del
    /// despliegue, la IP es la que pone en <c>X-Forwarded-For</c> (<see cref="ClientAddress"/>).
    /// </summary>
    public static RateLimiterOptions AddLoginRateLimit(this RateLimiterOptions options) =>
        options.AddPolicy(AuthEndpoints.LoginRateLimitPolicy, context =>
        {
            var auth = context.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value;
            return RateLimitPartitions.FixedWindow("ip:" + ClientAddress.Of(context), auth.LoginAttemptsPerMinute, TimeSpan.FromMinutes(1));
        });

    private static void ConfigureIdentity(IdentityOptions identity, AuthOptions auth)
    {
        identity.User.RequireUniqueEmail = true;
        identity.Password.RequiredLength = AuthOptions.MinimumPasswordLength;
        identity.Lockout.AllowedForNewUsers = true;
        identity.Lockout.MaxFailedAccessAttempts = auth.MaxFailedAccessAttempts;
        identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(auth.LockoutMinutes);
    }

    private static void ConfigureSessionCookie(CookieAuthenticationOptions cookie, AuthOptions auth)
    {
        // "__Host-": solo HTTPS, sin dominio y con ruta "/"; ningún subdominio puede sobrescribirla.
        cookie.Cookie.Name = SessionCookieName;
        cookie.Cookie.HttpOnly = true;
        cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        cookie.Cookie.SameSite = SameSiteMode.Strict;
        cookie.Cookie.Path = "/";
        cookie.ExpireTimeSpan = TimeSpan.FromHours(auth.SessionHours);
        cookie.SlidingExpiration = true;

        // Es una API: sin sesión responde 401 (y el frontend lleva al login), no una redirección a una página.
        cookie.Events.OnRedirectToLogin = context => SetStatus(context.Response, StatusCodes.Status401Unauthorized);
        cookie.Events.OnRedirectToAccessDenied = context => SetStatus(context.Response, StatusCodes.Status403Forbidden);
    }

    private static Task SetStatus(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
