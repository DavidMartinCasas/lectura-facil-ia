// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Domain.Tests.Documents;

/// <summary>Reintentar una adaptación que ha fallado.</summary>
public sealed class DocumentRetryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RetryAdaptation_FromError_GoesBackToProcessingWithoutTheFailure()
    {
        var document = Failed();

        document.RetryAdaptation(Now.AddMinutes(5));

        document.Status.ShouldBe(DocumentStatus.Processing);
        document.FailureCode.ShouldBeNull();
        document.Stage.ShouldBeNull();
        document.UpdatedAt.ShouldBe(Now.AddMinutes(5));
        DocumentStatusTransitions.IsAwaitingAdaptation(document.Status).ShouldBeTrue();
    }

    [Fact]
    public void RetryAdaptation_WhenTheDocumentHasNotFailed_Throws()
    {
        var received = Submit();
        var processing = Submit();
        processing.StartProcessing(Now);

        Should.Throw<InvalidOperationException>(() => received.RetryAdaptation(Now));
        Should.Throw<InvalidOperationException>(() => processing.RetryAdaptation(Now));
    }

    private static Document Failed()
    {
        var document = Submit();
        document.StartProcessing(Now);
        document.FailProcessing("llm.provider_unavailable", Now);
        return document;
    }

    private static Document Submit() => Document.Submit(
        OwnerId.From(Guid.NewGuid()),
        DocumentTitle.Create("Bases"),
        OriginalText.Create("Texto de prueba suficientemente largo.", new TextLengthLimits(10, 100)),
        Now);
}
