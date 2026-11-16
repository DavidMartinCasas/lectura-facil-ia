// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Readability;
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Api.Documents;

/// <summary>
/// Estado de un documento y, si ya está adaptado, su última versión. El texto adaptado es texto plano: el cliente nunca
/// debe interpretarlo como HTML ni Markdown.
/// </summary>
/// <param name="Error">Motivo genérico si la adaptación ha fallado.</param>
/// <param name="Original">El texto original con sus problemas de lectura fácil.</param>
/// <param name="Readability">Índice INFLESZ del original y de la última versión; <c>null</c> si aún no hay adaptación.</param>
public sealed record DocumentResponse(
    Guid Id,
    string Title,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DocumentErrorResponse? Error,
    AdaptationResponse? Adaptation,
    OriginalTextResponse Original,
    ReadabilityComparisonResponse? Readability)
{
    public static DocumentResponse From(DocumentDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return new DocumentResponse(
            details.Id.Value,
            details.Title,
            details.Status.ToString(),
            details.CreatedAt,
            details.UpdatedAt,
            details.Failure is { } failure ? new DocumentErrorResponse(failure.Code, failure.Message) : null,
            details.Adaptation is { } version ? AdaptationResponse.From(version, details.Status) : null,
            OriginalTextResponse.From(details.OriginalText, details.OriginalFindings),
            details.Readability is { } readability ? ReadabilityComparisonResponse.From(readability) : null);
    }
}

/// <summary>
/// Texto original y sus problemas. Las posiciones son índices de carácter (UTF-16, fin excluido) sobre
/// <paramref name="Text"/>, que es texto plano: el cliente lo parte en trozos y nunca lo interpreta como HTML.
/// </summary>
public sealed record OriginalTextResponse(string Text, IReadOnlyList<OriginalFindingResponse> Findings)
{
    public static OriginalTextResponse From(string text, IReadOnlyList<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(findings);
        return new OriginalTextResponse(text, [.. findings.Select(OriginalFindingResponse.From)]);
    }
}

/// <param name="Guideline">Descripción de la pauta que incumple, por ejemplo "Frases cortas".</param>
/// <param name="Suggestion">Qué hacer para cumplirla.</param>
public sealed record OriginalFindingResponse(
    string GuidelineCode,
    string Guideline,
    string Fragment,
    int Start,
    int End,
    string Severity,
    string Suggestion)
{
    public static OriginalFindingResponse From(Finding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return new OriginalFindingResponse(
            finding.GuidelineCode,
            GuidelineCatalog.Find(finding.GuidelineCode).Description,
            finding.Fragment,
            finding.Start,
            finding.End,
            finding.Severity.ToString(),
            finding.Suggestion);
    }
}

/// <summary>Legibilidad (INFLESZ) antes y después. Es un valor aproximado.</summary>
/// <param name="Improvement">Puntos que gana la adaptación; negativo si es más difícil.</param>
public sealed record ReadabilityComparisonResponse(
    ReadabilityScoreResponse Original,
    ReadabilityScoreResponse Adapted,
    double Improvement)
{
    public static ReadabilityComparisonResponse From(ReadabilityComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        return new ReadabilityComparisonResponse(
            ReadabilityScoreResponse.From(comparison.Original),
            ReadabilityScoreResponse.From(comparison.Adapted),
            comparison.Improvement);
    }
}

/// <param name="Grade"><c>VeryDifficult</c>, <c>SomewhatDifficult</c>, <c>Normal</c>, <c>QuiteEasy</c> o <c>VeryEasy</c>.</param>
/// <param name="GradeLabel">El grado para mostrarlo, por ejemplo "Bastante fácil".</param>
public sealed record ReadabilityScoreResponse(double Value, string Grade, string GradeLabel)
{
    public static ReadabilityScoreResponse From(ReadabilityScore score)
    {
        ArgumentNullException.ThrowIfNull(score);
        return new ReadabilityScoreResponse(score.Value, score.Grade.ToString(), score.GradeLabel);
    }
}

/// <summary>Un documento de la lista "Mis documentos".</summary>
/// <param name="Provider">
/// Nombre visible del proveedor que hizo la última adaptación (el que configura quien despliega); <c>null</c> si aún no
/// hay ninguna. El modelo no se expone.
/// </param>
public sealed record DocumentListItemResponse(
    Guid Id,
    string Title,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Provider)
{
    public static DocumentListItemResponse From(DocumentSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new DocumentListItemResponse(
            summary.Id.Value,
            summary.Title,
            summary.Status.ToString(),
            summary.CreatedAt,
            summary.UpdatedAt,
            summary.Provider);
    }
}

public sealed record DocumentErrorResponse(string Code, string Message);

/// <param name="PendingHumanValidation">Siempre <c>true</c> hasta que una persona la valide: es un borrador.</param>
/// <param name="PromptVersion">Versión del prompt que la generó (<c>manual</c> si es una edición a mano). El modelo no se expone.</param>
/// <param name="Provider">
/// Nombre visible del proveedor que hizo la adaptación ("Servidor propio", "Azure (UE)"…), el que configura quien
/// despliega. Una edición a mano conserva el de la versión que se editó. El modelo no se expone.
/// </param>
/// <param name="PendingKeyFactCount">Avisos de datos clave sin marcar como revisados.</param>
/// <param name="CanExport"><c>false</c> mientras quede algún aviso de datos clave sin marcar como revisado.</param>
/// <param name="Guidelines">Informe de pautas de la versión; <c>null</c> si se creó antes de existir el revisor.</param>
/// <param name="Verification">Datos clave del original y su estado; <c>null</c> si se creó antes de existir el verificador.</param>
public sealed record AdaptationResponse(
    int Version,
    string Title,
    IReadOnlyList<AdaptedSectionResponse> Sections,
    IReadOnlyList<GlossaryTermResponse> Glossary,
    bool PendingHumanValidation,
    string PromptVersion,
    DateTimeOffset CreatedAt,
    GuidelineReportResponse? Guidelines,
    MeaningVerificationResponse? Verification,
    string Provider,
    int PendingKeyFactCount,
    bool CanExport)
{
    public static AdaptationResponse From(AdaptedVersion version, DocumentStatus status)
    {
        ArgumentNullException.ThrowIfNull(version);
        return new AdaptationResponse(
            version.Number,
            version.Title,
            [.. version.Sections.Select(section => new AdaptedSectionResponse(section.Question, section.Sentences))],
            [.. version.Glossary.Select(term => new GlossaryTermResponse(term.Term, term.Definition))],
            status is not (DocumentStatus.Validated or DocumentStatus.Published),
            version.Metadata.PromptVersion,
            version.CreatedAt,
            version.GuidelineReport is { } report ? GuidelineReportResponse.From(report) : null,
            version.MeaningVerification is { } verification ? MeaningVerificationResponse.From(verification) : null,
            version.Metadata.Provider,
            version.PendingKeyFactCount,
            version.PendingKeyFactCount == 0);
    }
}

public sealed record AdaptedSectionResponse(string Question, IReadOnlyList<string> Sentences);

public sealed record GlossaryTermResponse(string Term, string Definition);

/// <summary>
/// Pautas de lectura fácil que cumple la adaptación. Las posiciones de los hallazgos son índices de carácter (UTF-16,
/// fin excluido) sobre el texto adaptado sin glosario: el título, y cada pregunta y cada frase en su propia línea,
/// separadas por <c>\n</c>.
/// </summary>
/// <param name="Summary">Por ejemplo, "11 de 12 pautas cumplidas".</param>
public sealed record GuidelineReportResponse(
    string Summary,
    int CompliantCount,
    int TotalCount,
    IReadOnlyList<GuidelineResultResponse> Items)
{
    public static GuidelineReportResponse From(GuidelineReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new GuidelineReportResponse(
            report.Summary,
            report.CompliantCount,
            report.TotalCount,
            [.. report.Results.Select(GuidelineResultResponse.From)]);
    }
}

/// <param name="Status"><c>Compliant</c>, <c>NotCompliant</c> o <c>NotChecked</c> (no se ha podido comprobar).</param>
/// <param name="FindingCount">Hallazgos de la pauta; puede haber más que los que se devuelven en <paramref name="Findings"/>.</param>
public sealed record GuidelineResultResponse(
    string Code,
    string Description,
    string Severity,
    string Status,
    int FindingCount,
    IReadOnlyList<GuidelineFindingResponse> Findings)
{
    public static GuidelineResultResponse From(GuidelineResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var guideline = result.Guideline;
        return new GuidelineResultResponse(
            guideline.Code,
            guideline.Description,
            guideline.DefaultSeverity.ToString(),
            result.Status.ToString(),
            result.FindingCount,
            [.. result.Findings.Select(finding =>
                new GuidelineFindingResponse(finding.Fragment, finding.Start, finding.End, finding.Severity.ToString(), finding.Suggestion))]);
    }
}

public sealed record GuidelineFindingResponse(string Fragment, int Start, int End, string Severity, string Suggestion);

/// <summary>
/// Datos clave del original (plazos, importes, requisitos…) y cómo han quedado en la adaptación. Los avisos van
/// primero. Los valores son citas de los textos: texto plano que el cliente nunca debe interpretar como HTML.
/// </summary>
/// <param name="Status"><c>Verified</c>, o <c>NotVerified</c> si el verificador no ha podido comprobarla: entonces todas las cifras del original quedan para revisar.</param>
/// <param name="Summary">Por ejemplo, "5 conservados, 1 a revisar, 1 omitido".</param>
public sealed record MeaningVerificationResponse(
    string Status,
    string Summary,
    int ConservedCount,
    int ReviewCount,
    int OmittedCount,
    IReadOnlyList<KeyFactResponse> KeyFacts)
{
    public static MeaningVerificationResponse From(MeaningVerification verification)
    {
        ArgumentNullException.ThrowIfNull(verification);
        return new MeaningVerificationResponse(
            verification.Status.ToString(),
            verification.Summary,
            verification.ConservedCount,
            verification.ReviewCount,
            verification.OmittedCount,
            [.. verification.KeyFacts.Select((fact, index) => new KeyFactResponse(
                index,
                fact.Type.ToString(),
                fact.OriginalValue,
                fact.AdaptedValue,
                fact.Status.ToString(),
                fact.Reason.ToString(),
                fact.ReasonMessage,
                fact.Acknowledged,
                fact.RequiresAcknowledgement))]);
    }
}

/// <param name="Id">Posición del dato en la lista de su versión (desde 0): es el <c>factId</c> para marcarlo como revisado.</param>
/// <param name="Type"><c>Deadline</c>, <c>Amount</c>, <c>Requirement</c>, <c>Date</c>, <c>Place</c>, <c>Document</c>, <c>Reference</c>, <c>Figure</c> (cifra que ha encontrado el código) u <c>Other</c>.</param>
/// <param name="AdaptedValue">Dónde está en la adaptación; <c>null</c> si no aparece.</param>
/// <param name="Status"><c>Conserved</c>, <c>Review</c> u <c>Omitted</c>.</param>
/// <param name="Reason">Código estable del motivo (<c>None</c> si se conserva).</param>
/// <param name="ReasonMessage">El motivo para mostrarlo, por ejemplo "El valor ha cambiado". Vacío si se conserva.</param>
/// <param name="Acknowledged">Una persona ha marcado el aviso como revisado.</param>
/// <param name="RequiresAcknowledgement">Es un aviso sin marcar como revisado: impide exportar.</param>
public sealed record KeyFactResponse(
    int Id,
    string Type,
    string OriginalValue,
    string? AdaptedValue,
    string Status,
    string Reason,
    string ReasonMessage,
    bool Acknowledged,
    bool RequiresAcknowledgement);

/// <summary>Evento SSE <c>status</c>: el estado actual del documento.</summary>
public sealed record DocumentStatusEvent(Guid Id, string Status);
