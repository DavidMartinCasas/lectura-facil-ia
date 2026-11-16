// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Users;

public enum SeedOutcome
{
    /// <summary>No hay usuario de prueba configurado.</summary>
    NotConfigured,

    /// <summary>Ya existía: no se toca, ni siquiera su contraseña.</summary>
    AlreadyExists,

    Created,
}
