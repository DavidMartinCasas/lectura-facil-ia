// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;

namespace LecturaFacil.Api.Security;

/// <summary>Inicio de sesión, bloqueo por intentos y duración de la sesión (US-15). Los valores por defecto son los de la historia.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Política mínima de contraseñas (US-15). Fija: no se puede bajar por configuración.</summary>
    public const int MinimumPasswordLength = 12;

    /// <summary>Intentos fallidos seguidos tras los que la cuenta se bloquea.</summary>
    [Range(3, 20, ErrorMessage = "Auth:MaxFailedAccessAttempts debe estar entre {1} y {2}.")]
    public int MaxFailedAccessAttempts { get; set; } = 5;

    [Range(1, 1_440, ErrorMessage = "Auth:LockoutMinutes debe estar entre {1} y {2}.")]
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Caducidad deslizante de la sesión: se renueva mientras se usa y caduca tras este tiempo sin usarla.</summary>
    [Range(1, 24, ErrorMessage = "Auth:SessionHours debe estar entre {1} y {2}.")]
    public int SessionHours { get; set; } = 8;

    /// <summary>Peticiones a <c>POST /api/auth/login</c> por minuto y dirección IP (OWASP API2 y API4).</summary>
    [Range(1, 1_000, ErrorMessage = "Auth:LoginAttemptsPerMinute debe estar entre {1} y {2}.")]
    public int LoginAttemptsPerMinute { get; set; } = 10;
}
