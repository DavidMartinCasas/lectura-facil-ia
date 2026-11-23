// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;

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

    /// <summary>
    /// Fase de la adaptación mientras el documento está en <see cref="DocumentStatus.Processing"/>; <c>null</c> en
    /// cualquier otro estado.
    /// </summary>
    public ProcessingStage? Stage { get; private set; }

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
    /// Guarda la adaptación, su informe de pautas y la verificación de su significado como una versión nueva y deja el documento en <see cref="DocumentStatus.Draft"/>.
    /// </summary>
    public AdaptedVersion CompleteAdaptation(
        AdaptedContent content,
        AdaptationMetadata metadata,
        GuidelineReport guidelineReport,
        MeaningVerification meaningVerification,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(guidelineReport);
        ArgumentNullException.ThrowIfNull(meaningVerification);
        if (Status != DocumentStatus.Processing)
        {
            throw new InvalidOperationException($"A document in {Status} cannot receive an adaptation.");
        }

        var version = AdaptedVersion.Create((LatestAdaptedVersion?.Number ?? 0) + 1, content, metadata, guidelineReport, meaningVerification, now);
        _adaptedVersions.Add(version);
        TransitionTo(DocumentStatus.Draft, now);
        return version;
    }

    /// <summary>
    /// Solo se edita y se revisa un borrador ya adaptado: después, el documento está en manos de quien lo valida.
    /// </summary>
    public bool IsEditable => Status == DocumentStatus.Draft && LatestAdaptedVersion is not null;

    /// <summary>
    /// Guarda una edición manual como una versión nueva, con su propio informe de pautas y sus datos clave recalculados
    /// (US-13). El documento sigue en <see cref="DocumentStatus.Draft"/>: una edición nunca valida nada.
    /// </summary>
    public AdaptedVersion SaveManualEdit(
        AdaptedContent content,
        AdaptationMetadata metadata,
        GuidelineReport guidelineReport,
        MeaningVerification meaningVerification,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(guidelineReport);
        ArgumentNullException.ThrowIfNull(meaningVerification);
        var latest = EnsureEditable();

        var version = AdaptedVersion.Create(latest.Number + 1, content, metadata, guidelineReport, meaningVerification, now);
        _adaptedVersions.Add(version);
        TransitionTo(DocumentStatus.Draft, now);
        return version;
    }

    /// <summary>Marca como revisado un aviso de los datos clave de la última versión.</summary>
    public void AcknowledgeKeyFact(int keyFactIndex, DateTimeOffset now)
    {
        EnsureEditable().AcknowledgeKeyFact(keyFactIndex);
        TransitionTo(DocumentStatus.Draft, now);
    }

    /// <summary>Marca como revisados todos los avisos de los datos clave de la última versión.</summary>
    public void AcknowledgeAllKeyFacts(DateTimeOffset now)
    {
        EnsureEditable().AcknowledgeAllKeyFacts();
        TransitionTo(DocumentStatus.Draft, now);
    }

    /// <summary>Pasa a otra fase de la adaptación. Solo mientras el documento está en <see cref="DocumentStatus.Processing"/>.</summary>
    public void EnterStage(ProcessingStage stage, DateTimeOffset now)
    {
        if (Status != DocumentStatus.Processing)
        {
            throw new InvalidOperationException($"A document in {Status} has no processing stage.");
        }

        Stage = stage;
        UpdatedAt = now;
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
        if (next != DocumentStatus.Processing)
        {
            Stage = null;
        }
    }

    private AdaptedVersion EnsureEditable() =>
        IsEditable ? LatestAdaptedVersion! : throw new InvalidOperationException($"A document in {Status} cannot be edited.");
}
