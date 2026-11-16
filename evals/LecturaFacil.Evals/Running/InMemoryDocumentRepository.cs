// SPDX-License-Identifier: Apache-2.0
using System.Collections.Concurrent;
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Evals.Running;

/// <summary>
/// Repositorio en memoria: la evaluación usa los mismos casos de uso que producción sin base de datos. Los documentos
/// solo viven mientras dura la ejecución.
/// </summary>
internal sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentDictionary<DocumentId, Document> _documents = new();

    public Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        _documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task<Document?> FindAsync(DocumentId id, CancellationToken cancellationToken) =>
        Task.FromResult(_documents.GetValueOrDefault(id));

    public Task<Document?> FindForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(_documents.TryGetValue(id, out var document) && document.OwnerId == ownerId ? document : null);

    public Task<Document?> FindForOwnerToUpdateAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
        FindForOwnerAsync(id, ownerId, cancellationToken);

    public Task<DocumentStatus?> FindStatusForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(_documents.TryGetValue(id, out var document) && document.OwnerId == ownerId ? document.Status : (DocumentStatus?)null);

    public Task<IReadOnlyList<DocumentId>> ListAwaitingAdaptationAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DocumentId>>(
            [.. _documents.Values.Where(document => DocumentStatusTransitions.IsAwaitingAdaptation(document.Status)).Select(document => document.Id)]);

    public Task<IReadOnlyList<DocumentSummary>> ListForOwnerAsync(OwnerId ownerId, int maxCount, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DocumentSummary>>([]);

    public Task SaveAsync(Document document, CancellationToken cancellationToken) => AddAsync(document, cancellationToken);
}
