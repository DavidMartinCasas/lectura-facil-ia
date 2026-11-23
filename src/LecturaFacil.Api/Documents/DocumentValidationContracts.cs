// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Documents;

/// <summary>
/// Resultado de validar la última versión con el grupo destinatario.
/// </summary>
/// <param name="Version">Versión adaptada que leyó el grupo; debe ser la que está pendiente de validación.</param>
/// <param name="Outcome"><c>Approved</c> si el grupo lo entiende; <c>ChangesRequested</c> si pide cambios.</param>
/// <param name="ValidatedBy">Quién lo validó, por ejemplo "Grupo de lectura de la asociación", sin nombres de personas.</param>
/// <param name="Notes">Observaciones del grupo, opcionales. Texto plano.</param>
public sealed record RecordValidationRequest(int? Version, string? Outcome, string? ValidatedBy, string? Notes);

/// <summary>Última validación anotada de un documento.</summary>
/// <param name="Version">Versión adaptada que leyó el grupo.</param>
public sealed record ValidationResponse(int Version, string Outcome, string ValidatedBy, string? Notes, DateTimeOffset RecordedAt)
{
    public static ValidationResponse From(ValidationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new ValidationResponse(record.VersionNumber, record.Outcome.ToString(), record.ValidatedBy, record.Notes, record.RecordedAt);
    }
}
