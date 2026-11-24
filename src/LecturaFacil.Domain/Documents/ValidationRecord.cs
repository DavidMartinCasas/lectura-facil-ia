// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>
/// Resultado de validar una versión con personas del grupo destinatario (<c>VALIDACION</c> del modelo de datos). La
/// validación se hace fuera de la aplicación, normalmente en una sesión con el texto impreso; quien adaptó el documento
/// anota aquí qué grupo lo leyó, cuántas personas eran, qué decidió, qué partes les costó entender y sus observaciones.
/// No guarda nombres de personas.
/// </summary>
public sealed class ValidationRecord
{
    public const int ValidatedByMaxLength = 200;
    public const int NotesMaxLength = 2000;
    public const int MaxParticipants = 99;

    private ValidationRecord(
        int number,
        int versionNumber,
        ValidationOutcome outcome,
        string validatedBy,
        int? participants,
        IReadOnlyList<string> hardSections,
        bool hardGlossary,
        string? notes,
        DateTimeOffset recordedAt)
    {
        Number = number;
        VersionNumber = versionNumber;
        Outcome = outcome;
        ValidatedBy = validatedBy;
        Participants = participants;
        HardSections = hardSections;
        HardGlossary = hardGlossary;
        Notes = notes;
        RecordedAt = recordedAt;
    }

    /// <summary>Orden de la validación dentro del documento, desde 1.</summary>
    public int Number { get; private set; }

    /// <summary>Versión adaptada que leyó el grupo.</summary>
    public int VersionNumber { get; private set; }

    public ValidationOutcome Outcome { get; private set; }

    /// <summary>Quién la validó, por ejemplo "Grupo de lectura de la asociación", sin nombres de personas.</summary>
    public string ValidatedBy { get; private set; }

    /// <summary>Cuántas personas del grupo la leyeron; <c>null</c> en las validaciones anotadas antes de pedirlo.</summary>
    public int? Participants { get; private set; }

    /// <summary>
    /// Preguntas de las secciones que costó entender, tal y como estaban en la versión que leyó el grupo, en su orden.
    /// Solo hay alguna si el grupo pidió cambios.
    /// </summary>
    public IReadOnlyList<string> HardSections { get; private set; }

    /// <summary>Si al grupo le costó entender el glosario. Solo puede ser cierto si pidió cambios.</summary>
    public bool HardGlossary { get; private set; }

    /// <summary>Observaciones del grupo; <c>null</c> si no hay.</summary>
    public string? Notes { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    /// <summary>
    /// Anota la validación de <paramref name="version"/>. Las secciones difíciles son posiciones dentro de sus secciones
    /// y se guardan como sus preguntas, para seguir sabiendo cuáles eran aunque el texto cambie después.
    /// </summary>
    internal static ValidationRecord Create(
        int number,
        AdaptedVersion version,
        ValidationOutcome outcome,
        string validatedBy,
        int participants,
        IReadOnlyCollection<int> hardSections,
        bool hardGlossary,
        string? notes,
        DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(validatedBy);
        ArgumentNullException.ThrowIfNull(hardSections);
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(participants, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(participants, MaxParticipants);

        var who = TextSanitizer.NormalizeSingleLine(validatedBy);
        if (who.Length == 0 || who.Length > ValidatedByMaxLength)
        {
            throw new ArgumentException("Who validated the document is required and must be short.", nameof(validatedBy));
        }

        var observations = notes is null ? null : TextSanitizer.NormalizeKeepingLineBreaks(notes).Trim();
        if (observations?.Length > NotesMaxLength)
        {
            throw new ArgumentException("The notes are too long.", nameof(notes));
        }

        if (!AreValidHardParts(version, outcome, hardSections, hardGlossary))
        {
            throw new ArgumentException(
                "The hard parts must exist in the version and can only be marked when changes are requested.",
                nameof(hardSections));
        }

        var questions = hardSections.Order().Select(index => version.Sections[index].Question).ToList().AsReadOnly();
        return new ValidationRecord(
            number,
            version.Number,
            outcome,
            who,
            participants,
            questions,
            hardGlossary,
            string.IsNullOrEmpty(observations) ? null : observations,
            recordedAt);
    }

    /// <summary>
    /// Las partes que costó entender solo se marcan cuando el grupo pide cambios, sin repetir y entre las que tiene la
    /// versión: sus secciones (por posición) y, si lo tiene, su glosario.
    /// </summary>
    public static bool AreValidHardParts(
        AdaptedVersion version,
        ValidationOutcome outcome,
        IReadOnlyCollection<int> hardSections,
        bool hardGlossary)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(hardSections);
        if (outcome == ValidationOutcome.Approved)
        {
            return hardSections.Count == 0 && !hardGlossary;
        }

        return hardSections.All(index => index >= 0 && index < version.Sections.Count)
            && hardSections.Distinct().Count() == hardSections.Count
            && (!hardGlossary || version.Glossary.Count > 0);
    }
}
