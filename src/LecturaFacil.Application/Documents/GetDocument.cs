// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Documents;

/// <summary>Estado de un documento del usuario actual y, si ya está adaptado, su última versión.</summary>
public sealed class GetDocument(IDocumentRepository repository, ICurrentUser currentUser)
{
    public async Task<Result<DocumentDetails>> ExecuteAsync(DocumentId id, CancellationToken cancellationToken)
    {
        var document = await repository.FindForOwnerAsync(id, currentUser.OwnerId, cancellationToken);
        if (document is null)
        {
            return Result<DocumentDetails>.Failure(DocumentErrors.NotFound);
        }

        var failure = document.Status == DocumentStatus.Error && document.FailureCode is { } code
            ? AdaptationErrors.FromFailureCode(code)
            : null;
        return Result<DocumentDetails>.Success(new DocumentDetails(
            document.Id,
            document.Title.Value,
            document.Status,
            document.CreatedAt,
            document.UpdatedAt,
            failure,
            document.LatestAdaptedVersion));
    }
}

/// <param name="Failure">Motivo genérico, para el usuario, si la adaptación ha fallado.</param>
/// <param name="Adaptation">Última versión adaptada, si la hay. Siempre es un borrador pendiente de validación humana.</param>
public sealed record DocumentDetails(
    DocumentId Id,
    string Title,
    DocumentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Error? Failure,
    AdaptedVersion? Adaptation);
