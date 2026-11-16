// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Users;

namespace LecturaFacil.Api.Security;

/// <summary>Crea el usuario de prueba al arrancar, después de las migraciones. Si no se puede crear, la API no arranca.</summary>
internal static partial class TestUserSeeding
{
    public static async Task SeedTestUserAsync(this WebApplication app, CancellationToken cancellationToken)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<SeedTestUser>().ExecuteAsync(cancellationToken);
        if (result.IsFailure)
        {
            // El mensaje solo lleva códigos de Identity, nunca el correo ni la contraseña.
            throw new InvalidOperationException(result.Error!.Message);
        }

        // Ni el correo ni la contraseña: solo qué ha pasado.
        TestUserSeeded(app.Logger, result.Value);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuario de prueba: {Outcome}.")]
    private static partial void TestUserSeeded(ILogger logger, SeedOutcome outcome);
}
