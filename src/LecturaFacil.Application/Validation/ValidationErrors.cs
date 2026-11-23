// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Validation;

/// <summary>Errores esperados al validar un documento. Los mensajes se muestran al usuario.</summary>
public static class ValidationErrors
{
    public static readonly Error PendingWarnings = new(
        "validation.pending_warnings",
        "Antes de enviarlo a validación, revisa los avisos de los datos clave.");

    public static readonly Error NotPending = new(
        "validation.not_pending",
        "El documento no está pendiente de validación.");

    public static readonly Error NotValidated = new(
        "validation.not_validated",
        "Solo se puede publicar un documento validado.");
}
