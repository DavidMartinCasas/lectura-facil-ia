// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>
/// Versión adaptada de un documento (<c>VERSION_ADAPTADA</c> del modelo de datos). Es un borrador: siempre necesita la
/// validación de una persona. No cambia una vez creada; una edición o una nueva adaptación crean otra versión.
/// </summary>
public sealed class AdaptedVersion
{
    private AdaptedVersion(int number, AdaptedContent content, AdaptationMetadata metadata, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(metadata);

        Number = number;
        Title = content.Title;
        Sections = content.Sections;
        Glossary = content.Glossary;
        Metadata = metadata;
        CreatedAt = createdAt;
    }

    /// <summary>Para reconstruir la versión desde la persistencia, que asigna <see cref="Metadata"/> después.</summary>
    private AdaptedVersion(
        int number,
        string title,
        IReadOnlyList<AdaptedSection> sections,
        IReadOnlyList<GlossaryTerm> glossary,
        DateTimeOffset createdAt)
    {
        Number = number;
        Title = title;
        Sections = sections;
        Glossary = glossary;
        Metadata = null!;
        CreatedAt = createdAt;
    }

    /// <summary>Número de versión dentro del documento, desde 1.</summary>
    public int Number { get; }

    public string Title { get; }

    public IReadOnlyList<AdaptedSection> Sections { get; }

    public IReadOnlyList<GlossaryTerm> Glossary { get; }

    public AdaptationMetadata Metadata { get; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Reconstruye una versión ya guardada.</summary>
    public static AdaptedVersion Restore(
        int number,
        string title,
        IReadOnlyList<AdaptedSection> sections,
        IReadOnlyList<GlossaryTerm> glossary,
        AdaptationMetadata metadata,
        DateTimeOffset createdAt) =>
        new(number, AdaptedContent.Restore(title, sections, glossary), metadata, createdAt);

    internal static AdaptedVersion Create(int number, AdaptedContent content, AdaptationMetadata metadata, DateTimeOffset createdAt) =>
        new(number, content, metadata, createdAt);
}
