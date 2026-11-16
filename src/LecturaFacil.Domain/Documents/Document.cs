// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>Documento que un usuario sube para adaptarlo a lectura fácil.</summary>
public sealed class Document
{
    private Document(
        DocumentId id,
        OwnerId ownerId,
        DocumentTitle title,
        OriginalText originalText,
        DocumentStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        OwnerId = ownerId;
        Title = title;
        OriginalText = originalText;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public DocumentId Id { get; }

    public OwnerId OwnerId { get; }

    public DocumentTitle Title { get; }

    public OriginalText OriginalText { get; }

    public DocumentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Crea un documento recién recibido.</summary>
    public static Document Submit(OwnerId ownerId, DocumentTitle title, OriginalText originalText, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(originalText);
        return new Document(DocumentId.New(), ownerId, title, originalText, DocumentStatus.Received, now);
    }

    public bool CanTransitionTo(DocumentStatus next) => DocumentStatusTransitions.IsAllowed(Status, next);

    /// <summary>Cambia de estado. Una transición fuera del ciclo de vida es un error de programación y lanza.</summary>
    public void TransitionTo(DocumentStatus next, DateTimeOffset now)
    {
        if (!CanTransitionTo(next))
        {
            throw new InvalidOperationException($"A document cannot go from {Status} to {next}.");
        }

        Status = next;
        UpdatedAt = now;
    }
}
