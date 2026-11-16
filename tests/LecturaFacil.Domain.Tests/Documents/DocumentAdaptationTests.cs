// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Domain.Tests.Documents;

public sealed class DocumentAdaptationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly AdaptationMetadata Metadata = new("Proveedor", "modelo", "adapter.v1", 100, 50, TimeSpan.FromSeconds(2));
    private static readonly GuidelineReport Report = GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10);

    [Fact]
    public void StartProcessing_WhenReceived_MovesToProcessing()
    {
        var document = Submit();

        document.StartProcessing(Now.AddMinutes(1));

        document.Status.ShouldBe(DocumentStatus.Processing);
        document.UpdatedAt.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public void StartProcessing_WhenAlreadyProcessing_KeepsProcessing()
    {
        var document = Submit();
        document.StartProcessing(Now);

        document.StartProcessing(Now.AddMinutes(1));

        document.Status.ShouldBe(DocumentStatus.Processing);
    }

    [Fact]
    public void StartProcessing_WhenInError_ClearsTheFailure()
    {
        var document = Submit();
        document.StartProcessing(Now);
        document.FailProcessing("llm.provider_unavailable", Now);

        document.StartProcessing(Now);

        document.Status.ShouldBe(DocumentStatus.Processing);
        document.FailureCode.ShouldBeNull();
    }

    [Fact]
    public void StartProcessing_WhenInDraft_Throws()
    {
        var document = Submit();
        document.StartProcessing(Now);
        document.CompleteAdaptation(Content(), Metadata, Report, Now);

        Should.Throw<InvalidOperationException>(() => document.StartProcessing(Now));
    }

    [Fact]
    public void CompleteAdaptation_AddsVersionOneAndMovesToDraft()
    {
        var document = Submit();
        document.StartProcessing(Now);
        var content = Content();

        var version = document.CompleteAdaptation(content, Metadata, Report, Now.AddMinutes(2));

        document.Status.ShouldBe(DocumentStatus.Draft);
        document.AdaptedVersions.ShouldBe([version]);
        document.LatestAdaptedVersion.ShouldBe(version);
        version.Number.ShouldBe(1);
        version.Title.ShouldBe(content.Title);
        version.Sections.ShouldBe(content.Sections);
        version.Glossary.ShouldBe(content.Glossary);
        version.Metadata.ShouldBe(Metadata);
        version.GuidelineReport.ShouldBe(Report);
        version.CreatedAt.ShouldBe(Now.AddMinutes(2));
    }

    [Fact]
    public void CompleteAdaptation_WhenNotProcessing_Throws() =>
        Should.Throw<InvalidOperationException>(() => Submit().CompleteAdaptation(Content(), Metadata, Report, Now));

    [Fact]
    public void FailProcessing_MovesToErrorWithTheReason()
    {
        var document = Submit();
        document.StartProcessing(Now);

        document.FailProcessing("llm.invalid_provider_output", Now);

        document.Status.ShouldBe(DocumentStatus.Error);
        document.FailureCode.ShouldBe("llm.invalid_provider_output");
        document.LatestAdaptedVersion.ShouldBeNull();
    }

    [Fact]
    public void FailProcessing_WhenTheReasonIsTooLong_Throws()
    {
        var document = Submit();
        document.StartProcessing(Now);

        Should.Throw<ArgumentException>(() => document.FailProcessing(new string('x', Document.FailureCodeMaxLength + 1), Now));
    }

    [Theory]
    [InlineData(DocumentStatus.Received, true)]
    [InlineData(DocumentStatus.Processing, true)]
    [InlineData(DocumentStatus.Error, false)]
    [InlineData(DocumentStatus.Draft, false)]
    public void IsAwaitingAdaptation_OnlyForReceivedOrProcessing(DocumentStatus status, bool expected) =>
        DocumentStatusTransitions.IsAwaitingAdaptation(status).ShouldBe(expected);

    [Fact]
    public void AdaptedVersion_Restore_RebuildsAStoredVersion()
    {
        var content = Content();

        var version = AdaptedVersion.Restore(3, content.Title, content.Sections, content.Glossary, Metadata, Report, Now);

        version.Number.ShouldBe(3);
        version.Sections.ShouldBe(content.Sections);
        version.GuidelineReport.ShouldBe(Report);
        version.PlainText.ShouldBe(content.PlainText);
    }

    private static AdaptedContent Content() =>
        AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Qué es?", ["Es una ayuda."])], [])!;

    private static Document Submit() => Document.Submit(
        OwnerId.From(Guid.NewGuid()),
        DocumentTitle.Create("Bases"),
        OriginalText.Create("Texto de prueba suficientemente largo.", new TextLengthLimits(10, 100)),
        Now);
}
