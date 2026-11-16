// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Llm;

/// <summary>Respuesta ya validada contra su esquema, con quién respondió y cuánto costó.</summary>
/// <param name="Value">Objeto tipado, validado contra el esquema JSON de <typeparamref name="T"/>.</param>
/// <param name="Provider">Nombre visible del proveedor que respondió.</param>
/// <param name="Model">Modelo (o despliegue) que respondió.</param>
/// <param name="Role">Si respondió el principal o el respaldo. Lo marca <see cref="ResilientLlmProvider"/>.</param>
public sealed record LlmResponse<T>(
    T Value,
    string Provider,
    string Model,
    long InputTokens,
    long OutputTokens,
    TimeSpan Duration,
    LlmProviderRole Role = LlmProviderRole.Primary);
