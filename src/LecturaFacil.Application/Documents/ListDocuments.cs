// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Documents;

/// <summary>Documentos del usuario actual, del más nuevo al más antiguo, hasta <see cref="LimitsOptions.MaxDocumentsPerList"/>.</summary>
public sealed class ListDocuments(IDocumentRepository repository, ICurrentUser currentUser, IOptions<LimitsOptions> limits)
{
    private readonly int _maxCount = limits.Value.MaxDocumentsPerList;

    public Task<IReadOnlyList<DocumentSummary>> ExecuteAsync(CancellationToken cancellationToken) =>
        repository.ListForOwnerAsync(currentUser.OwnerId, _maxCount, cancellationToken);
}

/// <summary>Un documento en la lista "Mis documentos".</summary>
/// <param name="Provider">Nombre visible del proveedor que generó la última adaptación; <c>null</c> si aún no hay ninguna.</param>
public sealed record DocumentSummary(
    DocumentId Id,
    string Title,
    DocumentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Provider)
{
    public static DocumentSummary From(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new DocumentSummary(
            document.Id,
            document.Title.Value,
            document.Status,
            document.CreatedAt,
            document.UpdatedAt,
            document.LatestAdaptedVersion?.Metadata.Provider);
    }
}
