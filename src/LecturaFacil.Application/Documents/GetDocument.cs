// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Readability;

namespace LecturaFacil.Application.Documents;

/// <summary>Estado de un documento del usuario actual y, si ya está adaptado, su última versión.</summary>
public sealed class GetDocument(IDocumentRepository repository, ICurrentUser currentUser, DescribeDocument describeDocument)
{
    public async Task<Result<DocumentDetails>> ExecuteAsync(DocumentId id, CancellationToken cancellationToken)
    {
        var document = await repository.FindForOwnerAsync(id, currentUser.OwnerId, cancellationToken);
        return document is null
            ? Result<DocumentDetails>.Failure(DocumentErrors.NotFound)
            : Result<DocumentDetails>.Success(describeDocument.Execute(document));
    }
}

/// <param name="Failure">Motivo genérico, para el usuario, si la adaptación ha fallado.</param>
/// <param name="Adaptation">Última versión adaptada, si la hay. Siempre es un borrador pendiente de validación humana.</param>
/// <param name="OriginalText">El texto original, al que se refieren las posiciones de <paramref name="OriginalFindings"/>.</param>
/// <param name="OriginalFindings">Problemas de lectura fácil del original.</param>
/// <param name="Editable">Un borrador ya adaptado: se puede editar y revisar (<see cref="Document.IsEditable"/>).</param>
/// <param name="CanSendToValidation">Un borrador con sus avisos revisados: se puede enviar a validación.</param>
/// <param name="LatestValidation">La última validación anotada, si la hay: quién la hizo, qué decidió y cuándo.</param>
/// <param name="CanRetry">La adaptación ha fallado y tiene sentido reintentarla (<see cref="Adaptation.RetryAdaptation"/>).</param>
/// <param name="Readability">
/// Índice INFLESZ del original y de la última versión; <c>null</c> si aún no hay adaptación o no se puede calcular.
/// </param>
public sealed record DocumentDetails(
    DocumentId Id,
    string Title,
    DocumentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Error? Failure,
    AdaptedVersion? Adaptation,
    string OriginalText,
    IReadOnlyList<Finding> OriginalFindings,
    ReadabilityComparison? Readability,
    bool Editable,
    bool CanRetry,
    bool CanSendToValidation,
    ValidationRecord? LatestValidation);
