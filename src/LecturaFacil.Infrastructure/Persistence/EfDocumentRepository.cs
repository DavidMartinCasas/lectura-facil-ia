// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Infrastructure.Persistence;

public sealed class EfDocumentRepository(AppDbContext context) : IDocumentRepository
{
    public async Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        context.Set<Document>().Add(document);
        await context.SaveChangesAsync(cancellationToken);
    }
}
