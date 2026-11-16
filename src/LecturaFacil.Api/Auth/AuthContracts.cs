// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Api.Auth;

/// <summary>Correo y contraseña. Nullable para que FluentValidation responda 400 en vez de fallar al deserializar.</summary>
internal sealed record LoginRequest(string? Email, string? Password);

/// <summary>Usuario con sesión. Solo el correo: nada de ids internos ni datos de la sesión.</summary>
internal sealed record CurrentUserResponse(string Email);
