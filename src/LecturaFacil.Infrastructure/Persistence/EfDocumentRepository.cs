// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace LecturaFacil.Infrastructure.Persistence;

public sealed class EfDocumentRepository(AppDbContext context) : IDocumentRepository
{
    private DbSet<Document> Documents => context.Set<Document>();

    public async Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        Documents.Add(document);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Document?> FindAsync(DocumentId id, CancellationToken cancellationToken) =>
        Documents.FirstOrDefaultAsync(document => document.Id == id, cancellationToken);

    public Task<Document?> FindForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
        Documents.AsNoTracking()
            .FirstOrDefaultAsync(document => document.Id == id && document.OwnerId == ownerId, cancellationToken);

    public Task<DocumentStatus?> FindStatusForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
        Documents.AsNoTracking()
            .Where(document => document.Id == id && document.OwnerId == ownerId)
            .Select(document => (DocumentStatus?)document.Status)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DocumentId>> ListAwaitingAdaptationAsync(CancellationToken cancellationToken) =>
        await Documents.AsNoTracking()
            .Where(document => document.Status == DocumentStatus.Received || document.Status == DocumentStatus.Processing)
            .OrderBy(document => document.CreatedAt)
            .Select(document => document.Id)
            .ToListAsync(cancellationToken);

    public async Task SaveAsync(Document document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        await context.SaveChangesAsync(cancellationToken);
    }
}
