// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Users;

/// <summary>Cuentas de usuario. Solo lo que necesita la siembra del usuario de prueba: no hay registro (US-15).</summary>
public interface IUserAccounts
{
    /// <summary>Si ya hay una cuenta con ese correo (sin distinguir mayúsculas).</summary>
    Task<bool> ExistsAsync(string email, CancellationToken cancellationToken);

    /// <summary>Crea la cuenta con la contraseña cifrada. Si no cumple la política de contraseñas, devuelve el error.</summary>
    Task<Result> CreateAsync(string email, string password, CancellationToken cancellationToken);
}
