// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Llm;
using LecturaFacil.Application.Tests.Documents;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Tests.Adaptation;

public sealed class RetryAdaptationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = OwnerId.From(Guid.NewGuid());

    private readonly InMemoryDocumentRepository _repository = new();
    private readonly RecordingAdaptationQueue _queue = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("llm.provider_unavailable")]
    [InlineData("llm.invalid_provider_output")]
    [InlineData("adaptation.failed")]
    [InlineData("algo.desconocido")]
    public async Task Retry_PutsTheFailedDocumentBackInTheQueue(string failureCode)
    {
        var document = Failed(Owner, failureCode);

        var result = await CreateUseCase().ExecuteAsync(document.Id, Token);

        result.Value.Status.ShouldBe(DocumentStatus.Processing);
        result.Value.Failure.ShouldBeNull();
        result.Value.CanRetry.ShouldBeFalse();
        _repository.SavedStatuses.ShouldBe([DocumentStatus.Processing]);
        _queue.Enqueued.ShouldBe([document.Id]);
    }

    [Fact]
    public async Task Retry_WhenTheDocumentIsOfAnotherUser_ReturnsNotFoundAndTouchesNothing()
    {
        var foreign = Failed(OwnerId.From(Guid.NewGuid()), LlmErrors.ProviderUnavailable.Code);

        var result = await CreateUseCase().ExecuteAsync(foreign.Id, Token);

        result.Error.ShouldBe(DocumentErrors.NotFound);
        foreign.Status.ShouldBe(DocumentStatus.Error);
        _queue.Enqueued.ShouldBeEmpty();
    }

    [Fact]
    public async Task Retry_WhenTheDocumentHasNotFailed_ReturnsNotFailed()
    {
        var received = Submit(Owner);

        var result = await CreateUseCase().ExecuteAsync(received.Id, Token);

        result.Error.ShouldBe(AdaptationErrors.NotFailed);
        _queue.Enqueued.ShouldBeEmpty();
    }

    [Fact]
    public async Task Retry_WhenTheTextIsTooLong_DoesNotRetryBecauseItWouldFailAgain()
    {
        var document = Failed(Owner, LlmErrors.InputTooLong.Code);

        var result = await CreateUseCase().ExecuteAsync(document.Id, Token);

        result.Error.ShouldBe(AdaptationErrors.NotRetriable);
        document.Status.ShouldBe(DocumentStatus.Error);
        _queue.Enqueued.ShouldBeEmpty();
    }

    [Fact]
    public async Task Retry_WhenAnotherRequestRetriedFirst_ReturnsNotFailedAndDoesNotQueueItTwice()
    {
        var document = Failed(Owner, LlmErrors.ProviderUnavailable.Code);
        _repository.FailNextSaveWithConflict = true;

        var result = await CreateUseCase().ExecuteAsync(document.Id, Token);

        result.Error.ShouldBe(AdaptationErrors.NotFailed);
        _queue.Enqueued.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("llm.provider_unavailable", true)]
    [InlineData("adaptation.failed", true)]
    [InlineData("llm.input_too_long", false)]
    public void Describe_OffersToRetryOnlyTheFailuresThatRetryingCanFix(string failureCode, bool canRetry)
    {
        var document = Failed(Owner, failureCode);

        DomainReadabilityCalculator.Describe().Execute(document).CanRetry.ShouldBe(canRetry);
        DomainReadabilityCalculator.Describe().Execute(Submit(Owner)).CanRetry.ShouldBeFalse();
    }

    private Document Failed(OwnerId owner, string failureCode)
    {
        var document = Submit(owner);
        document.StartProcessing(Now);
        document.FailProcessing(failureCode, Now);
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

    private RetryAdaptation CreateUseCase() =>
        new(_repository, new FixedCurrentUser(Owner), _queue, DomainReadabilityCalculator.Describe(), new FixedTimeProvider(Now));
}
