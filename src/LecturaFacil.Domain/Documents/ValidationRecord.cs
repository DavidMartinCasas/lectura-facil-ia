// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>
/// Resultado de validar una versión con personas del grupo destinatario (<c>VALIDACION</c> del modelo de datos). La
/// validación se hace fuera de la aplicación, normalmente en una sesión con el texto impreso; quien adaptó el documento
/// anota aquí qué grupo lo leyó, qué decidió y sus observaciones. No guarda nombres de personas.
/// </summary>
public sealed class ValidationRecord
{
    public const int ValidatedByMaxLength = 200;
    public const int NotesMaxLength = 2000;

    private ValidationRecord(
        int number,
        int versionNumber,
        ValidationOutcome outcome,
        string validatedBy,
        string? notes,
        DateTimeOffset recordedAt)
    {
        Number = number;
        VersionNumber = versionNumber;
        Outcome = outcome;
        ValidatedBy = validatedBy;
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

    /// <summary>Observaciones del grupo; <c>null</c> si no hay.</summary>
    public string? Notes { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    internal static ValidationRecord Create(
        int number,
        int versionNumber,
        ValidationOutcome outcome,
        string validatedBy,
        string? notes,
        DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(validatedBy);
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

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

        return new ValidationRecord(
            number,
            versionNumber,
            outcome,
            who,
            string.IsNullOrEmpty(observations) ? null : observations,
            recordedAt);
    }
}
