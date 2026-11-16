// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Documents;

public interface IDocumentRepository
{
    /// <summary>Guarda un documento nuevo.</summary>
    Task AddAsync(Document document, CancellationToken cancellationToken);

    /// <summary>
    /// Documento completo para modificarlo, sin filtrar por propietario. Solo para procesos internos (el worker de
    /// adaptación); las peticiones de un usuario usan <see cref="FindForOwnerAsync"/>.
    /// </summary>
    Task<Document?> FindAsync(DocumentId id, CancellationToken cancellationToken);

    /// <summary>Documento de solo lectura si existe y es de <paramref name="ownerId"/>; si no, <c>null</c> (BOLA).</summary>
    Task<Document?> FindForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken);

    /// <summary>
    /// Documento completo para modificarlo si existe y es de <paramref name="ownerId"/>; si no, <c>null</c> (BOLA). Los
    /// cambios se guardan con <see cref="SaveAsync"/>.
    /// </summary>
    Task<Document?> FindForOwnerToUpdateAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken);

    /// <summary>Solo el estado actual, si el documento existe y es de <paramref name="ownerId"/>.</summary>
    Task<DocumentStatus?> FindStatusForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken);

    /// <summary>Documentos que esperan su adaptación (recibidos o a medias tras un reinicio), del más antiguo al más nuevo.</summary>
    Task<IReadOnlyList<DocumentId>> ListAwaitingAdaptationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Resumen de los documentos de <paramref name="ownerId"/>, del más nuevo al más antiguo, como mucho
    /// <paramref name="maxCount"/>. Nunca devuelve documentos de otro usuario (BOLA).
    /// </summary>
    Task<IReadOnlyList<DocumentSummary>> ListForOwnerAsync(OwnerId ownerId, int maxCount, CancellationToken cancellationToken);

    /// <summary>Guarda los cambios de un documento obtenido con <see cref="FindAsync"/> o <see cref="FindForOwnerToUpdateAsync"/>.</summary>
    Task SaveAsync(Document document, CancellationToken cancellationToken);
}
