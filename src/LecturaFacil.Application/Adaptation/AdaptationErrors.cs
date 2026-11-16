// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;

namespace LecturaFacil.Application.Adaptation;

/// <summary>Errores esperados al adaptar un documento. Los mensajes son genéricos: sin modelos, proveedores ni servidores.</summary>
public static class AdaptationErrors
{
    public static readonly Error Failed = new(
        "adaptation.failed",
        "No se ha podido adaptar el documento. Inténtalo de nuevo más tarde.");

    public static readonly Error NotAwaitingAdaptation = new(
        "adaptation.not_awaiting",
        "El documento ya no está pendiente de adaptar.");

    private static readonly Dictionary<string, Error> KnownFailures = new[]
    {
        LlmErrors.InvalidProviderOutput,
        LlmErrors.ProviderUnavailable,
        LlmErrors.InputTooLong,
        Failed,
    }.ToDictionary(error => error.Code, StringComparer.Ordinal);

    /// <summary>Error que se muestra al usuario para el motivo guardado en un documento.</summary>
    public static Error FromFailureCode(string failureCode) => KnownFailures.GetValueOrDefault(failureCode, Failed);
}
