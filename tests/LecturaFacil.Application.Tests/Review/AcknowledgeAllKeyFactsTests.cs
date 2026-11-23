// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Review;
using LecturaFacil.Application.Tests.Documents;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Application.Tests.Review;

public sealed class AcknowledgeAllKeyFactsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = OwnerId.From(Guid.NewGuid());

    private readonly InMemoryDocumentRepository _repository = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AcknowledgeAll_MarksEveryWarningAsReviewed_InASingleSave()
    {
        var document = Adapted(Owner);

        var result = await CreateUseCase().ExecuteAsync(new AcknowledgeAllKeyFactsCommand(document.Id, 1), Token);

        var version = result.Value.Adaptation.ShouldNotBeNull();
        var facts = version.MeaningVerification!.KeyFacts;
        facts.Select(fact => fact.Acknowledged).ShouldBe([true, false, true]);
        facts[0].ReasonMessage.ShouldBe("No aparece en la adaptación");
        version.PendingKeyFactCount.ShouldBe(0);
        version.Number.ShouldBe(1);
        _repository.SavedStatuses.ShouldBe([DocumentStatus.Draft]);
    }

    [Fact]
    public async Task AcknowledgeAll_WhenNothingIsPending_DoesNotSave()
    {
        var document = Adapted(Owner);
        document.AcknowledgeAllKeyFacts(Now);

        var result = await CreateUseCase().ExecuteAsync(new AcknowledgeAllKeyFactsCommand(document.Id, 1), Token);

        result.IsSuccess.ShouldBeTrue();
        _repository.SavedStatuses.ShouldBeEmpty();
    }

    [Fact]
    public async Task AcknowledgeAll_WhenAnotherRequestChangedTheDocument_ReturnsAConflict()
    {
        var document = Adapted(Owner);
        _repository.FailNextSaveWithConflict = true;

        var result = await CreateUseCase().ExecuteAsync(new AcknowledgeAllKeyFactsCommand(document.Id, 1), Token);

        result.Error.ShouldBe(ReviewErrors.VersionConflict);
    }

    [Fact]
    public async Task AcknowledgeAll_WhenTheVersionIsNotTheLatest_ReturnsAConflict()
    {
        var document = Adapted(Owner);

        var result = await CreateUseCase().ExecuteAsync(new AcknowledgeAllKeyFactsCommand(document.Id, 2), Token);

        result.Error.ShouldBe(ReviewErrors.VersionConflict);
        document.LatestAdaptedVersion!.PendingKeyFactCount.ShouldBe(2);
    }

    [Fact]
    public async Task AcknowledgeAll_WhenTheDocumentIsNotADraft_ReturnsNotEditable()
    {
        var document = Submit(Owner);

        var result = await CreateUseCase().ExecuteAsync(new AcknowledgeAllKeyFactsCommand(document.Id, 1), Token);

        result.Error.ShouldBe(ReviewErrors.NotEditable);
    }

    [Fact]
    public async Task AcknowledgeAll_WhenTheDocumentBelongsToSomeoneElse_ReturnsNotFound()
    {
        var document = Adapted(OwnerId.From(Guid.NewGuid()));

        var result = await CreateUseCase().ExecuteAsync(new AcknowledgeAllKeyFactsCommand(document.Id, 1), Token);

        result.Error.ShouldBe(DocumentErrors.NotFound);
        document.LatestAdaptedVersion!.PendingKeyFactCount.ShouldBe(2);
    }

    private Document Adapted(OwnerId owner)
    {
        var document = Submit(owner);
        document.StartProcessing(Now);
        document.CompleteAdaptation(
            AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Qué es?", ["Una ayuda."])], [])!,
            new AdaptationMetadata("Servidor propio", "modelo", "adapter.v1", 1, 1, TimeSpan.FromSeconds(1)),
            GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10),
            MeaningVerification.Restore(
                VerificationStatus.Verified,
                [
                    KeyFact.Create(KeyFactType.Amount, "250 €", null, new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation)),
                    KeyFact.Create(KeyFactType.Other, "ayuda", "Una ayuda", KeyFactVerdict.Conserved),
                    KeyFact.Create(KeyFactType.Date, "1 de marzo", null, new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation)),
                ],
                null),
            Now);
        return document;
    }

    private Document Submit(OwnerId owner)
    {
        var document = Document.Submit(
            owner,
            DocumentTitle.Create("Bases"),
            OriginalText.Create("Una ayuda de 250 € para el alquiler.", new TextLengthLimits(10, 100)),
            Now);
        _repository.Added.Add(document);
        return document;
    }

    private AcknowledgeAllKeyFacts CreateUseCase() =>
        new(_repository, new FixedCurrentUser(Owner), DomainReadabilityCalculator.Describe(), new FixedTimeProvider(Now));
}
