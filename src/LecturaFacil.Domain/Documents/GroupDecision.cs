// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>
/// Lo que anota quien adaptó el documento después de la sesión con el grupo destinatario.
/// </summary>
/// <param name="Outcome">Si el grupo lo entiende o pide cambios.</param>
/// <param name="ValidatedBy">Qué grupo lo leyó, sin nombres de personas.</param>
/// <param name="Participants">Cuántas personas del grupo lo leyeron.</param>
/// <param name="HardSections">Posiciones de las secciones que costó entender. Solo si pide cambios.</param>
/// <param name="HardGlossary">Si costó entender el glosario. Solo si pide cambios.</param>
/// <param name="Notes">Observaciones del grupo, opcionales.</param>
public sealed record GroupDecision(
    ValidationOutcome Outcome,
    string ValidatedBy,
    int Participants,
    IReadOnlyCollection<int> HardSections,
    bool HardGlossary,
    string? Notes)
{
    /// <summary>El grupo lo entiende: no hay partes difíciles.</summary>
    public static GroupDecision Approved(string validatedBy, int participants, string? notes) =>
        new(ValidationOutcome.Approved, validatedBy, participants, [], false, notes);
}
