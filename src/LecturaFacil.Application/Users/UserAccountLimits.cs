// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Users;

/// <summary>Longitudes máximas de las credenciales de cualquier cuenta: login y usuario de prueba.</summary>
public static class UserAccountLimits
{
    /// <summary>Longitud máxima de un correo (RFC 5321) y de la columna de Identity.</summary>
    public const int MaxEmailLength = 256;

    public const int MaxPasswordLength = 128;
}
