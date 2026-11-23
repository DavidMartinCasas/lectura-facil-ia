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
    private readonly List<ValidationRecord> _validations = [];

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

    /// <summary>Validaciones anotadas, de la primera a la última.</summary>
    public IReadOnlyList<ValidationRecord> Validations => _validations.OrderBy(validation => validation.Number).ToList().AsReadOnly();

    public ValidationRecord? LatestValidation => _validations.MaxBy(validation => validation.Number);

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
    /// Vuelve a poner en marcha una adaptación que ha fallado: el documento pasa a <see cref="DocumentStatus.Processing"/>
    /// sin motivo de fallo, para que el worker lo adapte de nuevo. Solo desde <see cref="DocumentStatus.Error"/>.
    /// </summary>
    public void RetryAdaptation(DateTimeOffset now)
    {
        if (Status != DocumentStatus.Error)
        {
            throw new InvalidOperationException($"A document in {Status} cannot retry its adaptation.");
        }

        StartProcessing(now);
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

    /// <summary>
    /// Un borrador se envía a validación cuando una persona ya ha revisado los avisos de sus datos clave: es lo mismo que
    /// pide la exportación, y el grupo de validación necesita el documento exportado.
    /// </summary>
    public bool CanSendToValidation => IsEditable && LatestAdaptedVersion!.CanExport;

    /// <summary>
    /// Envía la última versión a validación: el documento pasa a <see cref="DocumentStatus.PendingValidation"/> y deja de
    /// poder editarse hasta que se anote el resultado o se retire.
    /// </summary>
    public void SendToValidation(DateTimeOffset now)
    {
        if (!CanSendToValidation)
        {
            throw new InvalidOperationException($"A document in {Status} with pending warnings cannot be sent to validation.");
        }

        TransitionTo(DocumentStatus.PendingValidation, now);
    }

    /// <summary>Retira el documento de la validación sin anotar ningún resultado: vuelve a ser un borrador editable.</summary>
    public void WithdrawFromValidation(DateTimeOffset now)
    {
        EnsureStatus(DocumentStatus.PendingValidation);
        TransitionTo(DocumentStatus.Draft, now);
    }

    /// <summary>
    /// Anota el resultado de la validación de la última versión. Si el grupo lo entiende, el documento queda
    /// <see cref="DocumentStatus.Validated"/>; si pide cambios, vuelve a <see cref="DocumentStatus.Draft"/>.
    /// </summary>
    public ValidationRecord RecordValidation(ValidationOutcome outcome, string validatedBy, string? notes, DateTimeOffset now)
    {
        EnsureStatus(DocumentStatus.PendingValidation);

        var record = ValidationRecord.Create(
            (LatestValidation?.Number ?? 0) + 1,
            LatestAdaptedVersion!.Number,
            outcome,
            validatedBy,
            notes,
            now);
        _validations.Add(record);
        TransitionTo(outcome == ValidationOutcome.Approved ? DocumentStatus.Validated : DocumentStatus.Draft, now);
        return record;
    }

    /// <summary>Publica la versión validada: es la versión final y ya no cambia.</summary>
    public void Publish(DateTimeOffset now)
    {
        EnsureStatus(DocumentStatus.Validated);
        TransitionTo(DocumentStatus.Published, now);
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

    private void EnsureStatus(DocumentStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"The document is in {Status}, not in {expected}.");
        }
    }

    private AdaptedVersion EnsureEditable() =>
        IsEditable ? LatestAdaptedVersion! : throw new InvalidOperationException($"A document in {Status} cannot be edited.");
}
