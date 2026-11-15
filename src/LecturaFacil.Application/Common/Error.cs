// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Common;

/// <summary>Error esperado de un caso de uso. El mensaje se puede mostrar al usuario.</summary>
/// <param name="Code">Código estable para la API, por ejemplo <c>document.not_found</c>.</param>
/// <param name="Message">Mensaje en español, sin detalles internos.</param>
public sealed record Error(string Code, string Message);
