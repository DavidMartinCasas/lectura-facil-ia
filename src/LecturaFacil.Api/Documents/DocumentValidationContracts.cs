// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Documents;

/// <summary>
/// Resultado de validar la última versión con el grupo destinatario.
/// </summary>
/// <param name="Version">Versión adaptada que leyó el grupo; debe ser la que está pendiente de validación.</param>
/// <param name="Outcome"><c>Approved</c> si el grupo lo entiende; <c>ChangesRequested</c> si pide cambios.</param>
/// <param name="ValidatedBy">Quién lo validó, por ejemplo "Grupo de lectura de la asociación", sin nombres de personas.</param>
/// <param name="Participants">Cuántas personas del grupo lo leyeron, de 1 a 99.</param>
/// <param name="HardSections">
/// Posiciones (desde 0) de las secciones que costó entender. Solo si el grupo pide cambios; si no, vacío o ausente.
/// </param>
/// <param name="HardGlossary">Si costó entender el glosario. Solo si el grupo pide cambios.</param>
/// <param name="Notes">Observaciones del grupo, opcionales. Texto plano.</param>
public sealed record RecordValidationRequest(
    int? Version,
    string? Outcome,
    string? ValidatedBy,
    int? Participants,
    IReadOnlyList<int>? HardSections,
    bool? HardGlossary,
    string? Notes);

/// <summary>Una validación anotada de un documento: una ronda con el grupo destinatario.</summary>
/// <param name="Round">Número de la ronda, desde 1.</param>
/// <param name="Version">Versión adaptada que leyó el grupo.</param>
/// <param name="Participants">Cuántas personas la leyeron; <c>null</c> en las anotadas antes de pedirlo.</param>
/// <param name="HardSections">Preguntas de las secciones que costó entender, como estaban en esa versión.</param>
/// <param name="HardGlossary">Si costó entender el glosario.</param>
public sealed record ValidationResponse(
    int Round,
    int Version,
    string Outcome,
    string ValidatedBy,
    int? Participants,
    IReadOnlyList<string> HardSections,
    bool HardGlossary,
    string? Notes,
    DateTimeOffset RecordedAt)
{
    public static ValidationResponse From(ValidationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new ValidationResponse(
            record.Number,
            record.VersionNumber,
            record.Outcome.ToString(),
            record.ValidatedBy,
            record.Participants,
            record.HardSections,
            record.HardGlossary,
            record.Notes,
            record.RecordedAt);
    }
}
