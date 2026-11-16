// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;
using LecturaFacil.Infrastructure.Adaptation;

namespace LecturaFacil.Infrastructure.Tests.Adaptation;

public sealed class ChannelAdaptationQueueTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Read_ReturnsTheDocumentsInTheOrderTheyWereQueued()
    {
        var queue = new ChannelAdaptationQueue();
        var first = DocumentId.New();
        var second = DocumentId.New();

        await queue.EnqueueAsync(first, Token);
        await queue.EnqueueAsync(second, Token);

        (await Take(queue, 2)).ShouldBe([first, second]);
    }

    [Fact]
    public async Task Enqueue_WhenTheDocumentIsAlreadyWaiting_DoesNotRepeatIt()
    {
        var queue = new ChannelAdaptationQueue();
        var id = DocumentId.New();
        var other = DocumentId.New();

        await queue.EnqueueAsync(id, Token);
        await queue.EnqueueAsync(id, Token);
        await queue.EnqueueAsync(other, Token);

        (await Take(queue, 2)).ShouldBe([id, other]);
    }

    [Fact]
    public async Task Enqueue_AfterTheDocumentLeftTheQueue_QueuesItAgain()
    {
        var queue = new ChannelAdaptationQueue();
        var id = DocumentId.New();
        await queue.EnqueueAsync(id, Token);
        await Take(queue, 1);

        await queue.EnqueueAsync(id, Token);

        (await Take(queue, 1)).ShouldBe([id]);
    }

    private static async Task<List<DocumentId>> Take(ChannelAdaptationQueue queue, int count)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var read = new List<DocumentId>();
        await foreach (var id in queue.ReadAllAsync(timeout.Token))
        {
            read.Add(id);
            if (read.Count == count)
            {
                break;
            }
        }

        return read;
    }
}
