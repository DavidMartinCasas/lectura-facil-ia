// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Llm;

/// <summary>
/// Puerto hacia un proveedor de modelos. Los casos de uso no saben qué proveedor responde.
/// Los fallos esperados (proveedor caído, tiempo agotado, salida que no cumple el esquema) llegan como
/// <see cref="Result"/> de error, nunca como excepción.
/// </summary>
public interface ILlmProvider
{
    /// <summary>Nombre visible del proveedor.</summary>
    string Name { get; }

    /// <summary>Pide una respuesta en JSON con el esquema de <typeparamref name="T"/> y la devuelve ya validada.</summary>
    /// <returns>
    /// La respuesta, o <see cref="LlmErrors.InvalidProviderOutput"/> si no cumple el esquema, o
    /// <see cref="LlmErrors.ProviderUnavailable"/> si el proveedor falla o no responde a tiempo.
    /// </returns>
    Task<Result<LlmResponse<T>>> CompleteStructuredAsync<T>(LlmRequest request, CancellationToken cancellationToken);
}
