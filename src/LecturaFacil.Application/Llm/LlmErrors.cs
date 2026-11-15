// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Llm;

/// <summary>Errores esperados al llamar a un modelo. Los mensajes no nombran proveedores, modelos ni servidores.</summary>
public static class LlmErrors
{
    public static readonly Error InvalidProviderOutput = new(
        "llm.invalid_provider_output",
        "El servicio de IA ha devuelto una respuesta que no se puede usar. Inténtalo de nuevo.");

    public static readonly Error ProviderUnavailable = new(
        "llm.provider_unavailable",
        "El servicio de IA no está disponible en este momento. Inténtalo más tarde.");
}
