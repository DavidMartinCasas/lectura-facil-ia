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

    public static readonly Error NotFailed = new(
        "adaptation.not_failed",
        "Solo se puede reintentar un documento cuya adaptación ha fallado.");

    public static readonly Error NotRetriable = new(
        "adaptation.not_retriable",
        "Este documento no se puede volver a adaptar tal como está. Envíalo de nuevo desde «Nuevo documento».");

    private static readonly Dictionary<string, Error> KnownFailures = new[]
    {
        LlmErrors.InvalidProviderOutput,
        LlmErrors.ProviderUnavailable,
        LlmErrors.InputTooLong,
        Failed,
    }.ToDictionary(error => error.Code, StringComparer.Ordinal);

    /// <summary>
    /// Si tiene sentido volver a intentar la adaptación con el mismo texto: sí cuando el servicio falló o respondió mal;
    /// no cuando el texto es demasiado largo, porque volvería a fallar igual.
    /// </summary>
    public static bool IsRetriable(string? failureCode) =>
        failureCode is not null && !string.Equals(failureCode, LlmErrors.InputTooLong.Code, StringComparison.Ordinal);

    /// <summary>Error que se muestra al usuario para el motivo guardado en un documento.</summary>
    public static Error FromFailureCode(string failureCode) => KnownFailures.GetValueOrDefault(failureCode, Failed);
}
