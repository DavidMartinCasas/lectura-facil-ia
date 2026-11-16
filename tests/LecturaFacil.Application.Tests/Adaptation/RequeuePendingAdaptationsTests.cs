// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Tests.Documents;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Tests.Adaptation;

public sealed class RequeuePendingAdaptationsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Requeue_QueuesReceivedAndProcessingDocumentsOnly()
    {
        var repository = new InMemoryDocumentRepository();
        var received = Add(repository);
        var processing = Add(repository);
        processing.StartProcessing(Now);
        var failed = Add(repository);
        failed.StartProcessing(Now);
        failed.FailProcessing("llm.provider_unavailable", Now);
        var queue = new RecordingAdaptationQueue();

        var count = await new RequeuePendingAdaptations(repository, queue).ExecuteAsync(TestContext.Current.CancellationToken);

        count.ShouldBe(2);
        queue.Enqueued.ShouldBe([received.Id, processing.Id], ignoreOrder: true);
    }

    private static Document Add(InMemoryDocumentRepository repository)
    {
        var document = Document.Submit(
            OwnerId.From(Guid.NewGuid()),
            DocumentTitle.Create("Bases"),
            OriginalText.Create("Texto de prueba suficientemente largo.", new TextLengthLimits(10, 100)),
            Now);
        repository.Added.Add(document);
        return document;
    }
}
