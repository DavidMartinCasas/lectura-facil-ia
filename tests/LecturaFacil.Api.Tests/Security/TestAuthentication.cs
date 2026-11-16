// SPDX-License-Identifier: Apache-2.0
using System.Security.Claims;
using System.Text.Encodings.Web;
using LecturaFacil.Domain.Documents;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Api.Tests.Security;

/// <summary>Usuarios de los tests que no prueban el inicio de sesión real.</summary>
public static class TestUsers
{
    public static readonly Guid Default = new("0b6e1c52-6b1f-4c43-9f0c-6d1d2f9b0a01");
    public static readonly Guid Other = new("0b6e1c52-6b1f-4c43-9f0c-6d1d2f9b0a02");

    public static OwnerId DefaultOwner => OwnerId.From(Default);

    public static OwnerId OtherOwner => OwnerId.From(Other);
}

/// <summary>
/// Sustituye la cookie de sesión en los tests que no prueban el inicio de sesión: autentica con el usuario de la
/// cabecera <see cref="UserHeader"/> (por defecto <see cref="TestUsers.Default"/>; <see cref="Anonymous"/> = sin sesión).
/// Antiforgery, autorización y el resto de la tubería son los reales.
/// </summary>
public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";
    public const string Anonymous = "anonymous";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers[UserHeader].ToString();
        if (header == Anonymous)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Guid.TryParse(header, out var parsed) ? parsed : TestUsers.Default;
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, $"{userId}@example.org"),
                new Claim(ClaimTypes.Email, $"{userId}@example.org"),
            ],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public static class TestAuthentication
{
    public const string AntiforgeryProbePath = "/api/auth/me";

    public static IServiceCollection UseTestAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
        return services;
    }
}
