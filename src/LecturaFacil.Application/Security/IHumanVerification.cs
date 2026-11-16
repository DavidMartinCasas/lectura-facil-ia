// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Security;

/// <summary>
/// Comprueba en el servidor que quien envía la petición es una persona (desafío anti-bots, US-16). Si la comprobación no
/// se puede hacer (error de red, tiempo agotado), falla: nunca se deja pasar la petición por defecto.
/// </summary>
public interface IHumanVerification
{
    /// <summary><c>true</c> si hay que pedir un token al navegador.</summary>
    bool IsEnabled { get; }

    Task<Result> VerifyAsync(string? token, CancellationToken cancellationToken);
}
