// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Tests.Documents;

public sealed class WatchDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = OwnerId.From(Guid.NewGuid());

    private readonly InMemoryDocumentRepository _repository = new();
    private readonly LimitsOptions _limits = new() { DocumentEventsPollMilliseconds = 100, DocumentEventsMaxSeconds = 10 };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Watch_SendsEachStatusChangeUntilTheAdaptationEnds()
    {
        var document = Add(Owner);
        foreach (var status in new[] { DocumentStatus.Received, DocumentStatus.Processing, DocumentStatus.Processing, DocumentStatus.Draft })
        {
            _repository.ScriptedProgress.Enqueue(new DocumentProgress(status, null));
        }

        var result = await CreateUseCase(TimeProvider.System).StartAsync(document.Id, Token);

        (await ReadAll(result.Value)).Select(progress => progress.Status)
            .ShouldBe([DocumentStatus.Received, DocumentStatus.Processing, DocumentStatus.Draft]);
    }

    [Fact]
    public async Task Watch_SendsEachStageChangeWhileProcessing()
    {
        var document = Add(Owner);
        DocumentProgress[] script =
        [
            new(DocumentStatus.Processing, ProcessingStage.Adapting),
            new(DocumentStatus.Processing, ProcessingStage.Adapting),
            new(DocumentStatus.Processing, ProcessingStage.ReviewingGuidelines),
            new(DocumentStatus.Processing, ProcessingStage.Verifying),
            new(DocumentStatus.Draft, null),
        ];
        foreach (var progress in script)
        {
            _repository.ScriptedProgress.Enqueue(progress);
        }

        var result = await CreateUseCase(TimeProvider.System).StartAsync(document.Id, Token);

        (await ReadAll(result.Value)).ShouldBe(
        [
            new DocumentProgress(DocumentStatus.Processing, ProcessingStage.Adapting),
            new DocumentProgress(DocumentStatus.Processing, ProcessingStage.ReviewingGuidelines),
            new DocumentProgress(DocumentStatus.Processing, ProcessingStage.Verifying),
            new DocumentProgress(DocumentStatus.Draft, null),
        ]);
    }

    [Fact]
    public async Task Watch_WhenTheDocumentIsAlreadyFinished_SendsItsStatusAndStops()
    {
        var document = Add(Owner);
        _repository.ScriptedProgress.Enqueue(new DocumentProgress(DocumentStatus.Error, null));

        var result = await CreateUseCase(TimeProvider.System).StartAsync(document.Id, Token);

        (await ReadAll(result.Value)).ShouldBe([new DocumentProgress(DocumentStatus.Error, null)]);
    }

    [Fact]
    public async Task Watch_StopsAfterTheMaximumDuration()
    {
        var document = Add(Owner);
        var time = new SteppingTimeProvider(Now, TimeSpan.FromSeconds(4));

        var result = await CreateUseCase(time).StartAsync(document.Id, Token);

        (await ReadAll(result.Value)).ShouldBe([new DocumentProgress(DocumentStatus.Received, null)]);
    }

    [Fact]
    public async Task Watch_WhenTheDocumentBelongsToSomeoneElse_ReturnsNotFound()
    {
        var document = Add(OwnerId.From(Guid.NewGuid()));

        var result = await CreateUseCase(TimeProvider.System).StartAsync(document.Id, Token);

        result.Error.ShouldBe(DocumentErrors.NotFound);
    }

    private static async Task<List<DocumentProgress>> ReadAll(IAsyncEnumerable<DocumentProgress> progress)
    {
        var read = new List<DocumentProgress>();
        await foreach (var item in progress)
        {
            read.Add(item);
        }

        return read;
    }

    private Document Add(OwnerId owner)
    {
        var document = Document.Submit(
            owner,
            DocumentTitle.Create("Bases"),
            OriginalText.Create("Texto de prueba suficientemente largo.", new TextLengthLimits(10, 100)),
            Now);
        _repository.Added.Add(document);
        return document;
    }

    private WatchDocument CreateUseCase(TimeProvider time) =>
        new(_repository, new FixedCurrentUser(Owner), Options.Create(_limits), time);

    /// <summary>Cada consulta de la hora avanza <c>step</c>: simula esperas largas sin esperar de verdad.</summary>
    private sealed class SteppingTimeProvider(DateTimeOffset start, TimeSpan step) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow()
        {
            var now = _now;
            _now += step;
            return now;
        }
    }
}
