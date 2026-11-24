// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Review;
using LecturaFacil.Application.Tests.Documents;
using LecturaFacil.Application.Validation;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Application.Tests.Validation;

/// <summary>Enviar a validación, anotar el resultado, retirar y publicar un documento del usuario actual.</summary>
public sealed class ValidationUseCasesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = OwnerId.From(Guid.NewGuid());

    private readonly InMemoryDocumentRepository _repository = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Send_AReviewedDraft_LeavesItPendingAndSaysSo()
    {
        var document = Reviewed(Owner);

        var result = await new SendToValidation(Step()).ExecuteAsync(new SendToValidationCommand(document.Id, 1), Token);

        result.Value.Status.ShouldBe(DocumentStatus.PendingValidation);
        result.Value.Editable.ShouldBeFalse();
        result.Value.CanSendToValidation.ShouldBeFalse();
        _repository.SavedStatuses.ShouldBe([DocumentStatus.PendingValidation]);
    }

    [Fact]
    public async Task Send_WithPendingKeyFacts_AsksToReviewThemFirst()
    {
        var document = Adapted(Owner);

        var result = await new SendToValidation(Step()).ExecuteAsync(new SendToValidationCommand(document.Id, 1), Token);

        result.Error.ShouldBe(ValidationErrors.PendingWarnings);
        _repository.SavedStatuses.ShouldBeEmpty();
    }

    [Fact]
    public async Task Send_WhenTheVersionIsNotTheLatest_ReturnsAConflict()
    {
        var document = Reviewed(Owner);

        var result = await new SendToValidation(Step()).ExecuteAsync(new SendToValidationCommand(document.Id, 2), Token);

        result.Error.ShouldBe(ReviewErrors.VersionConflict);
    }

    [Fact]
    public async Task Send_WhenItIsNotADraft_ReturnsNotEditable()
    {
        var document = Pending(Owner);

        var result = await new SendToValidation(Step()).ExecuteAsync(new SendToValidationCommand(document.Id, 1), Token);

        result.Error.ShouldBe(ReviewErrors.NotEditable);
    }

    [Fact]
    public async Task Record_Approved_ValidatesItAndReturnsTheValidation()
    {
        var document = Pending(Owner);

        var result = await new RecordValidation(Step()).ExecuteAsync(
            new RecordValidationCommand(document.Id, 1, new GroupDecision(ValidationOutcome.Approved, "Grupo de lectura", 4, [], false, "Se entiende.")),
            Token);

        result.Value.Status.ShouldBe(DocumentStatus.Validated);
        var validation = result.Value.LatestValidation.ShouldNotBeNull();
        validation.ValidatedBy.ShouldBe("Grupo de lectura");
        validation.Notes.ShouldBe("Se entiende.");
        validation.RecordedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task Record_ChangesRequested_ReturnsItToAnEditableDraftThatWaitsForChanges()
    {
        var document = Pending(Owner);

        var result = await new RecordValidation(Step()).ExecuteAsync(
            new RecordValidationCommand(document.Id, 1, new GroupDecision(ValidationOutcome.ChangesRequested, "Grupo", 4, [], false, "Falta explicar el plazo.")),
            Token);

        result.Value.Status.ShouldBe(DocumentStatus.Draft);
        result.Value.Editable.ShouldBeTrue();
        result.Value.CanSendToValidation.ShouldBeFalse();
        result.Value.LatestValidation!.Outcome.ShouldBe(ValidationOutcome.ChangesRequested);
    }

    [Fact]
    public async Task Record_WhenItIsNotPending_ReturnsNotPending()
    {
        var document = Reviewed(Owner);

        var result = await new RecordValidation(Step()).ExecuteAsync(
            new RecordValidationCommand(document.Id, 1, new GroupDecision(ValidationOutcome.Approved, "Grupo", 4, [], false, null)),
            Token);

        result.Error.ShouldBe(ValidationErrors.NotPending);
        document.Validations.ShouldBeEmpty();
    }

    [Fact]
    public async Task Record_ForAVersionThatIsNoLongerTheOnePending_ReturnsAConflictAndChangesNothing()
    {
        var document = Pending(Owner);

        var result = await new RecordValidation(Step()).ExecuteAsync(
            new RecordValidationCommand(document.Id, 2, new GroupDecision(ValidationOutcome.Approved, "Grupo", 4, [], false, null)),
            Token);

        result.Error.ShouldBe(ReviewErrors.VersionConflict);
        document.Status.ShouldBe(DocumentStatus.PendingValidation);
        document.Validations.ShouldBeEmpty();
        _repository.SavedStatuses.ShouldBeEmpty();
    }

    [Fact]
    public async Task Record_ChangesRequested_KeepsTheHardSectionsAndEveryRound()
    {
        var document = Pending(Owner);

        var result = await new RecordValidation(Step()).ExecuteAsync(
            new RecordValidationCommand(
                document.Id,
                1,
                new GroupDecision(ValidationOutcome.ChangesRequested, "Grupo", 3, [0], false, null)),
            Token);

        var validation = result.Value.Validations.ShouldHaveSingleItem();
        validation.Participants.ShouldBe(3);
        validation.HardSections.ShouldBe(["¿Qué es?"]);
        result.Value.LatestValidation.ShouldBe(validation);
    }

    [Theory]
    [InlineData(ValidationOutcome.ChangesRequested, 1, false)]
    [InlineData(ValidationOutcome.ChangesRequested, -1, true)]
    [InlineData(ValidationOutcome.Approved, 0, false)]
    public async Task Record_WithHardPartsThatAreNotInTheVersionOrNotAllowed_ReturnsUnknownPart(
        ValidationOutcome outcome,
        int section,
        bool glossary)
    {
        var document = Pending(Owner);
        int[] sections = section < 0 ? [] : [section];

        var result = await new RecordValidation(Step()).ExecuteAsync(
            new RecordValidationCommand(document.Id, 1, new GroupDecision(outcome, "Grupo", 3, sections, glossary, null)),
            Token);

        result.Error.ShouldBe(ValidationErrors.UnknownPart);
        document.Validations.ShouldBeEmpty();
        _repository.SavedStatuses.ShouldBeEmpty();
    }

    [Fact]
    public async Task Send_AfterTheGroupAskedForChangesWithoutEditingIt_SaysToChangeItFirst()
    {
        var document = Pending(Owner);
        document.RecordValidation(
            new GroupDecision(ValidationOutcome.ChangesRequested, "Grupo", 3, [], false, null),
            Now);

        var result = await new SendToValidation(Step()).ExecuteAsync(new SendToValidationCommand(document.Id, 1), Token);

        result.Error.ShouldBe(ValidationErrors.Unchanged);
        document.Status.ShouldBe(DocumentStatus.Draft);
    }

    [Fact]
    public async Task Withdraw_ReturnsItToDraft_AndOnlyWhenPending()
    {
        var document = Pending(Owner);

        (await new WithdrawFromValidation(Step()).ExecuteAsync(document.Id, Token)).Value.Status.ShouldBe(DocumentStatus.Draft);
        (await new WithdrawFromValidation(Step()).ExecuteAsync(document.Id, Token)).Error.ShouldBe(ValidationErrors.NotPending);
    }

    [Fact]
    public async Task Publish_OnlyAValidatedDocument()
    {
        var document = Pending(Owner);
        (await new PublishDocument(Step()).ExecuteAsync(document.Id, Token)).Error.ShouldBe(ValidationErrors.NotValidated);

        document.RecordValidation(GroupDecision.Approved("Grupo", 4, null), Now);
        var result = await new PublishDocument(Step()).ExecuteAsync(document.Id, Token);

        result.Value.Status.ShouldBe(DocumentStatus.Published);
        result.Value.Editable.ShouldBeFalse();
    }

    [Fact]
    public async Task EveryStep_WithADocumentOfAnotherUser_ReturnsNotFoundAndChangesNothing()
    {
        var foreign = Pending(OwnerId.From(Guid.NewGuid()));

        (await new SendToValidation(Step()).ExecuteAsync(new SendToValidationCommand(foreign.Id, 1), Token)).Error.ShouldBe(DocumentErrors.NotFound);
        (await new RecordValidation(Step()).ExecuteAsync(
            new RecordValidationCommand(foreign.Id, 1, new GroupDecision(ValidationOutcome.Approved, "Grupo", 4, [], false, null)), Token)).Error.ShouldBe(DocumentErrors.NotFound);
        (await new WithdrawFromValidation(Step()).ExecuteAsync(foreign.Id, Token)).Error.ShouldBe(DocumentErrors.NotFound);
        (await new PublishDocument(Step()).ExecuteAsync(foreign.Id, Token)).Error.ShouldBe(DocumentErrors.NotFound);
        foreign.Status.ShouldBe(DocumentStatus.PendingValidation);
        _repository.SavedStatuses.ShouldBeEmpty();
    }

    [Fact]
    public async Task AStep_WhenAnotherRequestChangedTheDocument_ReturnsAConflict()
    {
        var document = Pending(Owner);
        _repository.FailNextSaveWithConflict = true;

        var result = await new WithdrawFromValidation(Step()).ExecuteAsync(document.Id, Token);

        result.Error.ShouldBe(ReviewErrors.VersionConflict);
    }

    private Document Pending(OwnerId owner)
    {
        var document = Reviewed(owner);
        document.SendToValidation(Now);
        return document;
    }

    private Document Reviewed(OwnerId owner)
    {
        var document = Adapted(owner);
        document.AcknowledgeAllKeyFacts(Now);
        return document;
    }

    private Document Adapted(OwnerId owner)
    {
        var document = Document.Submit(
            owner,
            DocumentTitle.Create("Bases"),
            OriginalText.Create("Una ayuda de 250 € para el alquiler.", new TextLengthLimits(10, 100)),
            Now);
        document.StartProcessing(Now);
        document.CompleteAdaptation(
            AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Qué es?", ["Una ayuda."])], [])!,
            new AdaptationMetadata("Servidor propio", "modelo", "adapter.v1", 1, 1, TimeSpan.FromSeconds(1)),
            GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10),
            MeaningVerification.Restore(
                VerificationStatus.Verified,
                [KeyFact.Create(KeyFactType.Amount, "250 €", null, new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation))],
                null),
            Now);
        _repository.Added.Add(document);
        return document;
    }

    private ValidationStep Step() =>
        new(_repository, new FixedCurrentUser(Owner), DomainReadabilityCalculator.Describe(), new FixedTimeProvider(Now));
}
