// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Api.Documents;

/// <summary>
/// Estado de un documento y, si ya está adaptado, su última versión. El texto adaptado es texto plano: el cliente nunca
/// debe interpretarlo como HTML ni Markdown.
/// </summary>
/// <param name="Error">Motivo genérico si la adaptación ha fallado.</param>
public sealed record DocumentResponse(
    Guid Id,
    string Title,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DocumentErrorResponse? Error,
    AdaptationResponse? Adaptation)
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
            details.Adaptation is { } version ? AdaptationResponse.From(version, details.Status) : null);
    }
}

public sealed record DocumentErrorResponse(string Code, string Message);

/// <param name="PendingHumanValidation">Siempre <c>true</c> hasta que una persona la valide: es un borrador.</param>
/// <param name="PromptVersion">Versión del prompt que la generó. El modelo y el proveedor se guardan, pero no se exponen.</param>
/// <param name="Guidelines">Informe de pautas de la versión; <c>null</c> si se creó antes de existir el revisor.</param>
public sealed record AdaptationResponse(
    int Version,
    string Title,
    IReadOnlyList<AdaptedSectionResponse> Sections,
    IReadOnlyList<GlossaryTermResponse> Glossary,
    bool PendingHumanValidation,
    string PromptVersion,
    DateTimeOffset CreatedAt,
    GuidelineReportResponse? Guidelines)
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
            version.GuidelineReport is { } report ? GuidelineReportResponse.From(report) : null);
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

/// <summary>Evento SSE <c>status</c>: el estado actual del documento.</summary>
public sealed record DocumentStatusEvent(Guid Id, string Status);
