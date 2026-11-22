// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Domain.Tests.Documents;

public sealed class DocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = OwnerId.From(Guid.NewGuid());
    private static readonly OriginalText Text = OriginalText.Create("Texto de prueba suficientemente largo.", new TextLengthLimits(10, 100));

    [Fact]
    public void Submit_WhenCalled_CreatesADocumentInReceivedState()
    {
        var document = Submit();

        document.Id.Value.ShouldNotBe(Guid.Empty);
        document.OwnerId.ShouldBe(Owner);
        document.Title.Value.ShouldBe("Bases");
        document.OriginalText.ShouldBe(Text);
        document.Status.ShouldBe(DocumentStatus.Received);
        document.CreatedAt.ShouldBe(Now);
        document.UpdatedAt.ShouldBe(Now);
        document.OriginalFindings.ShouldBeEmpty();
    }

    [Fact]
    public void RecordOriginalFindings_ReplacesThePreviousFindings()
    {
        var document = Submit();
        var guideline = GuidelineCatalog.Find(GuidelineCodes.ShortSentences);
        var first = Finding.At(guideline, Text.Value, 0, 5, "Primera.");
        var second = Finding.At(guideline, Text.Value, 6, 8, "Segunda.");

        document.RecordOriginalFindings([first]);
        document.RecordOriginalFindings([second]);

        document.OriginalFindings.ShouldBe([second]);
    }

    [Fact]
    public void RecordOriginalFindings_WhenAFindingIsOutsideTheOriginalText_Throws()
    {
        var document = Submit();
        var longer = Text.Value + " y más texto";
        var outside = Finding.At(GuidelineCatalog.Find(GuidelineCodes.ShortSentences), longer, 0, longer.Length, "Fuera.");

        Should.Throw<ArgumentException>(() => document.RecordOriginalFindings([outside]));
    }

    [Theory]
    [InlineData(DocumentStatus.Received, DocumentStatus.Processing)]
    [InlineData(DocumentStatus.Processing, DocumentStatus.Error)]
    [InlineData(DocumentStatus.Error, DocumentStatus.Processing)]
    [InlineData(DocumentStatus.Processing, DocumentStatus.Draft)]
    [InlineData(DocumentStatus.Draft, DocumentStatus.Draft)]
    [InlineData(DocumentStatus.Draft, DocumentStatus.PendingValidation)]
    [InlineData(DocumentStatus.PendingValidation, DocumentStatus.Validated)]
    [InlineData(DocumentStatus.PendingValidation, DocumentStatus.Draft)]
    [InlineData(DocumentStatus.Validated, DocumentStatus.Published)]
    public void CanTransitionTo_WhenTransitionIsInTheLifecycle_ReturnsTrue(DocumentStatus from, DocumentStatus to)
    {
        DocumentStatusTransitions.IsAllowed(from, to).ShouldBeTrue();
    }

    [Theory]
    [InlineData(DocumentStatus.Received, DocumentStatus.Draft)]
    [InlineData(DocumentStatus.Received, DocumentStatus.Received)]
    [InlineData(DocumentStatus.Received, DocumentStatus.Published)]
    [InlineData(DocumentStatus.Processing, DocumentStatus.Received)]
    [InlineData(DocumentStatus.Error, DocumentStatus.Draft)]
    [InlineData(DocumentStatus.Draft, DocumentStatus.Validated)]
    [InlineData(DocumentStatus.Draft, DocumentStatus.Published)]
    [InlineData(DocumentStatus.PendingValidation, DocumentStatus.Published)]
    [InlineData(DocumentStatus.Validated, DocumentStatus.Draft)]
    [InlineData(DocumentStatus.Published, DocumentStatus.Draft)]
    [InlineData(DocumentStatus.Published, DocumentStatus.Published)]
    public void CanTransitionTo_WhenTransitionIsNotInTheLifecycle_ReturnsFalse(DocumentStatus from, DocumentStatus to)
    {
        DocumentStatusTransitions.IsAllowed(from, to).ShouldBeFalse();
    }

    [Fact]
    public void TransitionTo_WhenAllowed_ChangesStatusAndUpdateDate()
    {
        var document = Submit();
        var later = Now.AddMinutes(5);

        document.TransitionTo(DocumentStatus.Processing, later);

        document.Status.ShouldBe(DocumentStatus.Processing);
        document.UpdatedAt.ShouldBe(later);
        document.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void TransitionTo_WhenNotAllowed_ThrowsAndKeepsTheState()
    {
        var document = Submit();

        Should.Throw<InvalidOperationException>(() => document.TransitionTo(DocumentStatus.Published, Now.AddMinutes(1)));

        document.Status.ShouldBe(DocumentStatus.Received);
        document.UpdatedAt.ShouldBe(Now);
    }

    [Fact]
    public void CanTransitionTo_WhenCalledOnADocument_UsesItsCurrentStatus()
    {
        var document = Submit();

        document.CanTransitionTo(DocumentStatus.Processing).ShouldBeTrue();
        document.CanTransitionTo(DocumentStatus.Draft).ShouldBeFalse();
    }

    [Fact]
    public void EnterStage_WhileProcessing_KeepsTheStageUntilTheDocumentLeavesProcessing()
    {
        var document = Submit();
        document.StartProcessing(Now);

        document.EnterStage(ProcessingStage.ReviewingGuidelines, Now.AddSeconds(5));

        document.Stage.ShouldBe(ProcessingStage.ReviewingGuidelines);
        document.UpdatedAt.ShouldBe(Now.AddSeconds(5));

        document.FailProcessing("adaptation.failed", Now.AddSeconds(6));

        document.Stage.ShouldBeNull();
    }

    [Fact]
    public void EnterStage_WhenNotProcessing_Throws()
    {
        var document = Submit();

        Should.Throw<InvalidOperationException>(() => document.EnterStage(ProcessingStage.Adapting, Now));
        document.Stage.ShouldBeNull();
    }

    [Fact]
    public void Submit_WhenArgumentsAreNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Document.Submit(Owner, null!, Text, Now));
        Should.Throw<ArgumentNullException>(() => Document.Submit(Owner, DocumentTitle.Default, null!, Now));
    }

    private static Document Submit() => Document.Submit(Owner, DocumentTitle.Create("Bases"), Text, Now);
}
