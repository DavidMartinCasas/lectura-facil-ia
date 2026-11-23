// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Domain.Documents;

/// <summary>
/// Versión adaptada de un documento (<c>VERSION_ADAPTADA</c> del modelo de datos). Es un borrador: siempre necesita la
/// validación de una persona. Su contenido no cambia una vez creada; una edición o una nueva adaptación crean otra
/// versión. Lo único que cambia es qué avisos de sus datos clave ha marcado como revisados una persona.
/// </summary>
public sealed class AdaptedVersion
{
    private AdaptedVersion(
        int number,
        AdaptedContent content,
        AdaptationMetadata metadata,
        GuidelineReport? guidelineReport,
        MeaningVerification? meaningVerification,
        DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(metadata);

        Number = number;
        Title = content.Title;
        Sections = content.Sections;
        Glossary = content.Glossary;
        Metadata = metadata;
        GuidelineReport = guidelineReport;
        MeaningVerification = meaningVerification;
        CreatedAt = createdAt;
    }

    /// <summary>Para reconstruir la versión desde la persistencia, que asigna <see cref="Metadata"/> después.</summary>
    private AdaptedVersion(
        int number,
        string title,
        IReadOnlyList<AdaptedSection> sections,
        IReadOnlyList<GlossaryTerm> glossary,
        GuidelineReport? guidelineReport,
        MeaningVerification? meaningVerification,
        DateTimeOffset createdAt)
    {
        Number = number;
        Title = title;
        Sections = sections;
        Glossary = glossary;
        Metadata = null!;
        GuidelineReport = guidelineReport;
        MeaningVerification = meaningVerification;
        CreatedAt = createdAt;
    }

    /// <summary>Número de versión dentro del documento, desde 1.</summary>
    public int Number { get; }

    public string Title { get; }

    public IReadOnlyList<AdaptedSection> Sections { get; }

    public IReadOnlyList<GlossaryTerm> Glossary { get; }

    public AdaptationMetadata Metadata { get; }

    /// <summary>
    /// Pautas que cumple la versión, con posiciones sobre <see cref="PlainText"/>. <c>null</c> en las versiones creadas
    /// antes de que existiera el revisor de pautas.
    /// </summary>
    public GuidelineReport? GuidelineReport { get; }

    /// <summary>
    /// Datos clave del original y su estado en esta versión (US-11). <c>null</c> en las versiones creadas antes de que
    /// existiera el verificador.
    /// </summary>
    public MeaningVerification? MeaningVerification { get; private set; }

    /// <summary>Avisos de datos clave sin marcar como revisados. Mientras haya alguno, la versión no se puede exportar.</summary>
    public int PendingKeyFactCount => MeaningVerification?.PendingAcknowledgementCount ?? 0;

    /// <summary>
    /// Pautas que no se cumplen. Son orientativas: no se marcan como revisadas ni bloquean la exportación; dejan de
    /// contar cuando una edición las cumple.
    /// </summary>
    public int BrokenGuidelineCount => GuidelineReport is { } report ? report.TotalCount - report.CompliantCount : 0;

    /// <summary>Se puede exportar cuando una persona ha marcado como revisados todos los avisos de datos clave.</summary>
    public bool CanExport => PendingKeyFactCount == 0;

    /// <inheritdoc cref="AdaptedContent.PlainText"/>
    public string PlainText => AdaptedContent.ToPlainText(Title, Sections);

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Reconstruye una versión ya guardada.</summary>
    public static AdaptedVersion Restore(
        int number,
        string title,
        IReadOnlyList<AdaptedSection> sections,
        IReadOnlyList<GlossaryTerm> glossary,
        AdaptationMetadata metadata,
        GuidelineReport? guidelineReport,
        MeaningVerification? meaningVerification,
        DateTimeOffset createdAt) =>
        new(number, AdaptedContent.Restore(title, sections, glossary), metadata, guidelineReport, meaningVerification, createdAt);

    /// <summary>Marca como revisado el aviso del dato clave número <paramref name="keyFactIndex"/> (desde 0).</summary>
    internal void AcknowledgeKeyFact(int keyFactIndex)
    {
        var verification = MeaningVerification
            ?? throw new InvalidOperationException("This version has no key facts to acknowledge.");
        MeaningVerification = verification.AcknowledgeKeyFact(keyFactIndex);
    }

    /// <summary>Marca como revisados todos los avisos de los datos clave. Sin verificación, no hay nada que marcar.</summary>
    internal void AcknowledgeAllKeyFacts() => MeaningVerification = MeaningVerification?.AcknowledgeAllKeyFacts();

    internal static AdaptedVersion Create(
        int number,
        AdaptedContent content,
        AdaptationMetadata metadata,
        GuidelineReport guidelineReport,
        MeaningVerification meaningVerification,
        DateTimeOffset createdAt) =>
        new(number, content, metadata, guidelineReport, meaningVerification, createdAt);
}
