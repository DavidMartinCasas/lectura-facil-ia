// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LecturaFacil.Infrastructure.Persistence;

public sealed class EfDocumentRepository(AppDbContext context) : IDocumentRepository
{
    private const string LikeEscapeCharacter = "\\";

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

    public Task<Document?> FindForOwnerToUpdateAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
        Documents.FirstOrDefaultAsync(document => document.Id == id && document.OwnerId == ownerId, cancellationToken);

    public Task<DocumentStatus?> FindStatusForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
        Documents.AsNoTracking()
            .Where(document => document.Id == id && document.OwnerId == ownerId)
            .Select(document => (DocumentStatus?)document.Status)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<DocumentProgress?> FindProgressForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
        Documents.AsNoTracking()
            .Where(document => document.Id == id && document.OwnerId == ownerId)
            .Select(document => new DocumentProgress(document.Status, document.Stage, document.StagePart, document.StageParts))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DocumentId>> ListAwaitingAdaptationAsync(CancellationToken cancellationToken) =>
        await Documents.AsNoTracking()
            .Where(document => DocumentStatusTransitions.AwaitingAdaptation.Contains(document.Status))
            .OrderBy(document => document.CreatedAt)
            .Select(document => document.Id)
            .ToListAsync(cancellationToken);

    public async Task<DocumentPage> ListForOwnerAsync(OwnerId ownerId, DocumentListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var documents = Documents.AsNoTracking().Where(document => document.OwnerId == ownerId);

        if (query.TitleContains is { Length: > 0 } title)
        {
            // Sin distinguir mayúsculas (ILIKE) ni tildes (unaccent): "constitucion" encuentra "Constitución".
            var pattern = "%" + EscapeLikePattern(title) + "%";
            documents = documents.Where(document => EF.Functions.ILike(
                EF.Functions.Unaccent(EF.Property<string>(document, nameof(Document.Title))),
                EF.Functions.Unaccent(pattern),
                LikeEscapeCharacter));
        }

        if (query.Statuses.Count > 0)
        {
            var statuses = query.Statuses.ToArray();
            documents = documents.Where(document => statuses.Contains(document.Status));
        }

        if (query.After is { } after)
        {
            // Paginación por cursor (keyset): (fecha, id) < (fecha, id) del último documento ya mostrado.
            var afterId = DocumentId.From(after.Id);
            documents = documents.Where(document => EF.Functions.LessThan(
                ValueTuple.Create(document.CreatedAt, document.Id),
                ValueTuple.Create(after.CreatedAt, afterId)));
        }

        // Las columnas de la lista, el texto original y solo la última versión: de ellos salen los avisos y la legibilidad.
        var rows = await documents
            .OrderByDescending(document => document.CreatedAt)
            .ThenByDescending(document => document.Id)
            .Take(query.PageSize + 1)
            .Select(document => new
            {
                document.Id,
                document.Title,
                document.Status,
                document.CreatedAt,
                document.UpdatedAt,
                document.OriginalText,
                Latest = document.AdaptedVersions
                    .OrderByDescending(version => version.Number)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var summaries = rows
            .Select(row => DocumentSummary.Create(
                row.Id,
                row.Title.Value,
                row.Status,
                row.CreatedAt,
                row.UpdatedAt,
                row.OriginalText.Value,
                row.Latest))
            .ToList();
        return DocumentPage.FromFetched(summaries, query.PageSize);
    }

    public async Task SaveAsync(Document document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrentUpdateException("The document was changed by another request.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dos ediciones simultáneas de la misma versión crean la misma versión siguiente.
            throw new ConcurrentUpdateException("The document was changed by another request.", exception);
        }
    }

    /// <summary>El texto del usuario se busca literalmente: %, _ y \ no actúan como comodines.</summary>
    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
