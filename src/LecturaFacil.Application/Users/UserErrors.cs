// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Users;

public static class UserErrors
{
    public const string SeedFailedCode = "user.seed_failed";

    /// <summary>No se ha podido crear el usuario de prueba. Solo los códigos de error, nunca el correo ni la contraseña.</summary>
    public static Error SeedFailed(IEnumerable<string> reasons) => new(
        SeedFailedCode,
        $"No se ha podido crear el usuario de prueba: {string.Join(", ", reasons)}.");
}
