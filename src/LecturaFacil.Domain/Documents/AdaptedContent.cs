// SPDX-License-Identifier: Apache-2.0
using System.Globalization;

namespace LecturaFacil.Domain.Documents;

/// <summary>Sección de una adaptación sin validar, tal y como la propone el modelo.</summary>
public sealed record AdaptedSectionDraft(string Question, IReadOnlyList<string> Sentences);

/// <summary>Término del glosario sin validar, tal y como lo propone el modelo.</summary>
public sealed record GlossaryTermDraft(string Term, string Definition);

/// <summary>Sección de lectura fácil: una pregunta que hace de título y frases cortas que la responden.</summary>
public sealed record AdaptedSection(string Question, IReadOnlyList<string> Sentences);

/// <summary>Palabra difícil y su explicación sencilla.</summary>
public sealed record GlossaryTerm(string Term, string Definition);

/// <summary>
/// Texto adaptado a lectura fácil, como texto plano estructurado (nunca HTML ni Markdown que se renderice): título,
/// secciones con pregunta y frases, y glosario. Todo el texto está normalizado (NFC, sin caracteres de control, en una
/// línea) y nada está vacío.
/// </summary>
public sealed record AdaptedContent
{
    // Cultura fija: el resultado no depende del idioma del proceso que adapta.
    private static readonly StringComparer SpanishIgnoreCase = StringComparer.Create(CultureInfo.GetCultureInfo("es-ES"), ignoreCase: true);

    private AdaptedContent(string title, IReadOnlyList<AdaptedSection> sections, IReadOnlyList<GlossaryTerm> glossary)
    {
        Title = title;
        Sections = sections;
        Glossary = glossary;
    }

    public string Title { get; }

    public IReadOnlyList<AdaptedSection> Sections { get; }

    public IReadOnlyList<GlossaryTerm> Glossary { get; }

    /// <summary>
    /// Normaliza la propuesta del modelo. Las frases vacías se descartan. Devuelve <c>null</c> si falta el título, no hay
    /// secciones, una sección se queda sin pregunta o sin frases, o un término del glosario está incompleto.
    /// </summary>
    public static AdaptedContent? TryCreate(
        string title,
        IEnumerable<AdaptedSectionDraft> sections,
        IEnumerable<GlossaryTermDraft> glossary)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(glossary);

        var normalizedTitle = Clean(title);
        var normalizedSections = sections.Select(NormalizeSection).ToList();
        var normalizedGlossary = glossary.Select(NormalizeTerm).ToList();
        if (normalizedTitle.Length == 0
            || normalizedSections.Count == 0
            || normalizedSections.Exists(section => section is null)
            || normalizedGlossary.Exists(term => term is null))
        {
            return null;
        }

        return new AdaptedContent(normalizedTitle, normalizedSections.AsReadOnly()!, normalizedGlossary.AsReadOnly()!);
    }

    /// <summary>
    /// Une las adaptaciones de las partes de un texto largo, en orden: el título de la primera, todas las secciones y
    /// el glosario sin términos repetidos (se queda la primera definición).
    /// </summary>
    public static AdaptedContent Merge(IReadOnlyList<AdaptedContent> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count == 0)
        {
            throw new ArgumentException("There must be at least one part to merge.", nameof(parts));
        }

        var sections = parts.SelectMany(part => part.Sections).ToList();
        var glossary = parts.SelectMany(part => part.Glossary)
            .DistinctBy(term => term.Term, SpanishIgnoreCase)
            .ToList();
        return new AdaptedContent(parts[0].Title, sections.AsReadOnly(), glossary.AsReadOnly());
    }

    /// <summary>Reconstruye un contenido ya guardado, sin volver a normalizarlo.</summary>
    internal static AdaptedContent Restore(string title, IReadOnlyList<AdaptedSection> sections, IReadOnlyList<GlossaryTerm> glossary) =>
        new(title, sections, glossary);

    private static AdaptedSection? NormalizeSection(AdaptedSectionDraft section)
    {
        ArgumentNullException.ThrowIfNull(section);

        var question = Clean(section.Question);
        var sentences = (section.Sentences ?? []).Select(Clean).Where(sentence => sentence.Length > 0).ToList();
        return question.Length == 0 || sentences.Count == 0 ? null : new AdaptedSection(question, sentences.AsReadOnly());
    }

    private static GlossaryTerm? NormalizeTerm(GlossaryTermDraft term)
    {
        ArgumentNullException.ThrowIfNull(term);

        var name = Clean(term.Term);
        var definition = Clean(term.Definition);
        return name.Length == 0 || definition.Length == 0 ? null : new GlossaryTerm(name, definition);
    }

    private static string Clean(string? text) => text is null ? string.Empty : TextSanitizer.NormalizeSingleLine(text);
}
