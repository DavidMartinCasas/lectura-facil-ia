// SPDX-License-Identifier: Apache-2.0
using Microsoft.AspNetCore.Hosting;

namespace LecturaFacil.Api.Tests.Security;

/// <summary>
/// API con PostgreSQL y el inicio de sesión real de Identity (cookie de sesión). Siembra un usuario de prueba con una
/// contraseña generada en cada ejecución: ninguna contraseña va escrita en el repositorio.
/// </summary>
public sealed class IdentityApiFactory : ApiFactory
{
    public string SeededEmail { get; } = "persona.prueba@example.org";

    public string SeededPassword { get; } = NewPassword();

    protected override bool UseRealAuthentication => true;

    /// <summary>Cumple la política de Identity: 12 caracteres o más, mayúscula, minúscula, cifra y símbolo.</summary>
    public static string NewPassword() => $"Aa1!{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Seed:TestUser:Email", SeededEmail);
        builder.UseSetting("Seed:TestUser:Password", SeededPassword);

        // Todos los tests de la clase comparten el límite de intentos de login: aquí no es lo que se prueba.
        builder.UseSetting("Auth:LoginAttemptsPerMinute", "1000");
    }
}
