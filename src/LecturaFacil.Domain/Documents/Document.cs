// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Domain.Documents;

/// <summary>Documento que un usuario sube para adaptarlo a lectura fácil.</summary>
public sealed class Document
{
    /// <summary>Longitud máxima del código del motivo de un error.</summary>
    public const int FailureCodeMaxLength = 64;

    private readonly List<Finding> _originalFindings = [];
    private readonly List<AdaptedVersion> _adaptedVersions = [];

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

    /// <summary>Problemas de lectura fácil del texto original, con posiciones sobre <see cref="OriginalText"/>.</summary>
    public IReadOnlyList<Finding> OriginalFindings => _originalFindings.AsReadOnly();

    /// <summary>Versiones adaptadas, de la primera a la última.</summary>
    public IReadOnlyList<AdaptedVersion> AdaptedVersions => _adaptedVersions.OrderBy(version => version.Number).ToList().AsReadOnly();

    public AdaptedVersion? LatestAdaptedVersion => _adaptedVersions.MaxBy(version => version.Number);

    /// <summary>Código del motivo del último error (por ejemplo <c>llm.provider_unavailable</c>), sin detalles internos.</summary>
    public string? FailureCode { get; private set; }

    /// <summary>Crea un documento recién recibido.</summary>
    public static Document Submit(OwnerId ownerId, DocumentTitle title, OriginalText originalText, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(originalText);
        return new Document(DocumentId.New(), ownerId, title, originalText, DocumentStatus.Received, now);
    }

    /// <summary>Sustituye los problemas detectados en el texto original.</summary>
    public void RecordOriginalFindings(IEnumerable<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        var recorded = findings.ToList();
        if (recorded.Exists(finding => finding.End > OriginalText.Value.Length))
        {
            throw new ArgumentException("A finding points outside the original text.", nameof(findings));
        }

        _originalFindings.Clear();
        _originalFindings.AddRange(recorded);
    }

    /// <summary>
    /// Empieza (o retoma, si ya estaba en <see cref="DocumentStatus.Processing"/> al reiniciarse el servicio) la adaptación.
    /// </summary>
    public void StartProcessing(DateTimeOffset now)
    {
        if (Status == DocumentStatus.Processing)
        {
            return;
        }

        TransitionTo(DocumentStatus.Processing, now);
        FailureCode = null;
    }

    /// <summary>
    /// Guarda la adaptación y su informe de pautas como una versión nueva y deja el documento en <see cref="DocumentStatus.Draft"/>.
    /// </summary>
    public AdaptedVersion CompleteAdaptation(
        AdaptedContent content,
        AdaptationMetadata metadata,
        GuidelineReport guidelineReport,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(guidelineReport);
        if (Status != DocumentStatus.Processing)
        {
            throw new InvalidOperationException($"A document in {Status} cannot receive an adaptation.");
        }

        var version = AdaptedVersion.Create((LatestAdaptedVersion?.Number ?? 0) + 1, content, metadata, guidelineReport, now);
        _adaptedVersions.Add(version);
        TransitionTo(DocumentStatus.Draft, now);
        return version;
    }

    /// <summary>La adaptación ha fallado. <paramref name="failureCode"/> es un código estable, sin detalles internos.</summary>
    public void FailProcessing(string failureCode, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        if (failureCode.Length > FailureCodeMaxLength)
        {
            throw new ArgumentException("The failure code is too long.", nameof(failureCode));
        }

        TransitionTo(DocumentStatus.Error, now);
        FailureCode = failureCode;
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
