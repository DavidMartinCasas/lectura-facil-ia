// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;

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
public sealed record AdaptationResponse(
    int Version,
    string Title,
    IReadOnlyList<AdaptedSectionResponse> Sections,
    IReadOnlyList<GlossaryTermResponse> Glossary,
    bool PendingHumanValidation,
    string PromptVersion,
    DateTimeOffset CreatedAt)
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
            version.CreatedAt);
    }
}

public sealed record AdaptedSectionResponse(string Question, IReadOnlyList<string> Sentences);

public sealed record GlossaryTermResponse(string Term, string Definition);

/// <summary>Evento SSE <c>status</c>: el estado actual del documento.</summary>
public sealed record DocumentStatusEvent(Guid Id, string Status);
