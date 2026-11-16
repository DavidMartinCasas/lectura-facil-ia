// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Review;
using LecturaFacil.Application.Tests.Documents;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Application.Tests.Review;

public sealed class AcknowledgeKeyFactTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = OwnerId.From(Guid.NewGuid());

    private readonly InMemoryDocumentRepository _repository = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Acknowledge_MarksTheWarningAsReviewed_AndKeepsItVisible()
    {
        var document = Adapted(Owner);

        var result = await CreateUseCase().ExecuteAsync(new AcknowledgeKeyFactCommand(document.Id, 1, 0), Token);

        var version = result.Value.Adaptation.ShouldNotBeNull();
        var fact = version.MeaningVerification!.KeyFacts[0];
        fact.Acknowledged.ShouldBeTrue();
        fact.Status.ShouldBe(KeyFactStatus.Omitted);
        fact.ReasonMessage.ShouldBe("No aparece en la adaptación");
        version.PendingKeyFactCount.ShouldBe(0);
        version.Number.ShouldBe(1);
        _repository.SavedStatuses.ShouldBe([DocumentStatus.Draft]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public async Task Acknowledge_WhenTheKeyFactDoesNotExist_ReturnsKeyFactNotFound(int index)
    {
        var document = Adapted(Owner);

        var result = await CreateUseCase().ExecuteAsync(new AcknowledgeKeyFactCommand(document.Id, 1, index), Token);

        result.Error.ShouldBe(ReviewErrors.KeyFactNotFound);
        _repository.SavedStatuses.ShouldBeEmpty();
    }

    [Fact]
    public async Task Acknowledge_WhenAnotherRequestChangedTheDocument_ReturnsAConflict()
    {
        var document = Adapted(Owner);
        _repository.FailNextSaveWithConflict = true;

        var result = await CreateUseCase().ExecuteAsync(new AcknowledgeKeyFactCommand(document.Id, 1, 0), Token);

        result.Error.ShouldBe(ReviewErrors.VersionConflict);
    }

    [Fact]
    public async Task Acknowledge_WhenTheVersionIsNotTheLatest_ReturnsAConflict()
    {
        var document = Adapted(Owner);

        var result = await CreateUseCase().ExecuteAsync(new AcknowledgeKeyFactCommand(document.Id, 2, 0), Token);

        result.Error.ShouldBe(ReviewErrors.VersionConflict);
    }

    [Fact]
    public async Task Acknowledge_WhenTheDocumentIsNotADraft_ReturnsNotEditable()
    {
        var document = Submit(Owner);

        var result = await CreateUseCase().ExecuteAsync(new AcknowledgeKeyFactCommand(document.Id, 1, 0), Token);

        result.Error.ShouldBe(ReviewErrors.NotEditable);
    }

    [Fact]
    public async Task Acknowledge_WhenTheDocumentBelongsToSomeoneElse_ReturnsNotFound()
    {
        var document = Adapted(OwnerId.From(Guid.NewGuid()));

        var result = await CreateUseCase().ExecuteAsync(new AcknowledgeKeyFactCommand(document.Id, 1, 0), Token);

        result.Error.ShouldBe(DocumentErrors.NotFound);
        document.LatestAdaptedVersion!.PendingKeyFactCount.ShouldBe(1);
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

    private AcknowledgeKeyFact CreateUseCase() =>
        new(_repository, new FixedCurrentUser(Owner), DomainReadabilityCalculator.Describe(), new FixedTimeProvider(Now));
}
