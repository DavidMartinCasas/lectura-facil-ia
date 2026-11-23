// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Domain.Tests.Documents;

/// <summary>Edición manual y revisión de avisos de un borrador (US-13).</summary>
public sealed class DocumentReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly AdaptationMetadata Metadata = new("Proveedor", "modelo", "adapter.v1", 100, 50, TimeSpan.FromSeconds(2));
    private static readonly GuidelineReport Report = GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10);

    [Fact]
    public void SaveManualEdit_AddsTheNextVersionAndKeepsTheDraft()
    {
        var document = Adapted();
        var edited = Content("Es una ayuda para el alquiler.");

        var version = document.SaveManualEdit(edited, Metadata with { PromptVersion = "manual" }, Report, Verification(), Now.AddHours(1));

        version.Number.ShouldBe(2);
        version.Sections.ShouldBe(edited.Sections);
        document.LatestAdaptedVersion.ShouldBe(version);
        document.AdaptedVersions.Count.ShouldBe(2);
        document.AdaptedVersions[0].Sections[0].Sentences.ShouldBe(["Es una ayuda."]);
        document.Status.ShouldBe(DocumentStatus.Draft);
        document.UpdatedAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void SaveManualEdit_WhenTheDocumentIsNotADraft_Throws()
    {
        var document = Submit();

        document.IsEditable.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => document.SaveManualEdit(Content("Otra frase."), Metadata, Report, Verification(), Now));
    }

    [Fact]
    public void AcknowledgeKeyFact_MarksTheWarningOfTheLatestVersion()
    {
        var document = Adapted();
        document.LatestAdaptedVersion!.PendingKeyFactCount.ShouldBe(1);
        document.LatestAdaptedVersion.CanExport.ShouldBeFalse();

        document.AcknowledgeKeyFact(0, Now.AddMinutes(5));

        var fact = document.LatestAdaptedVersion!.MeaningVerification!.KeyFacts[0];
        fact.Acknowledged.ShouldBeTrue();
        fact.Status.ShouldBe(KeyFactStatus.Omitted);
        document.LatestAdaptedVersion.PendingKeyFactCount.ShouldBe(0);
        document.LatestAdaptedVersion.CanExport.ShouldBeTrue();
        document.UpdatedAt.ShouldBe(Now.AddMinutes(5));
    }

    [Fact]
    public void AcknowledgeAllKeyFacts_MarksEveryWarningOfTheLatestVersion_AndLeavesTheConservedOnesAsTheyWere()
    {
        var document = Submit();
        document.StartProcessing(Now);
        document.CompleteAdaptation(
            Content("Es una ayuda."),
            Metadata,
            Report,
            MeaningVerification.Restore(
                VerificationStatus.Verified,
                [
                    KeyFact.Create(KeyFactType.Amount, "250 €", null, new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation)),
                    KeyFact.Create(KeyFactType.Other, "ayuda", "Es una ayuda", KeyFactVerdict.Conserved),
                    KeyFact.Create(KeyFactType.Date, "1 de marzo", null, new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation)),
                ],
                null),
            Now);
        document.LatestAdaptedVersion!.PendingKeyFactCount.ShouldBe(2);

        document.AcknowledgeAllKeyFacts(Now.AddMinutes(5));

        var facts = document.LatestAdaptedVersion!.MeaningVerification!.KeyFacts;
        facts.Select(fact => fact.Acknowledged).ShouldBe([true, false, true]);
        facts.Select(fact => fact.Status).ShouldBe([KeyFactStatus.Omitted, KeyFactStatus.Conserved, KeyFactStatus.Omitted]);
        document.LatestAdaptedVersion.PendingKeyFactCount.ShouldBe(0);
        document.LatestAdaptedVersion.CanExport.ShouldBeTrue();
        document.UpdatedAt.ShouldBe(Now.AddMinutes(5));
    }

    [Fact]
    public void ReviewWarningCount_AddsThePendingKeyFactsToTheGuidelinesThatAreNotMet()
    {
        var document = Adapted();
        var brokenGuidelines = Report.TotalCount - Report.CompliantCount;

        document.LatestAdaptedVersion!.ReviewWarningCount.ShouldBe(brokenGuidelines + 1);

        document.AcknowledgeKeyFact(0, Now.AddMinutes(5));

        document.LatestAdaptedVersion!.ReviewWarningCount.ShouldBe(brokenGuidelines);
    }

    [Fact]
    public void AcknowledgeAllKeyFacts_WhenTheDocumentIsNotADraft_Throws() =>
        Should.Throw<InvalidOperationException>(() => Submit().AcknowledgeAllKeyFacts(Now));

    [Fact]
    public void AcknowledgeKeyFact_WhenTheFactDoesNotExist_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Adapted().AcknowledgeKeyFact(3, Now));

    [Fact]
    public void AcknowledgeKeyFact_WhenTheVersionHasNoVerification_Throws()
    {
        var document = Adapted();
        document.SaveManualEdit(Content("Otra frase."), Metadata, Report, MeaningVerification.Restore(VerificationStatus.Verified, [], null), Now);

        Should.Throw<ArgumentOutOfRangeException>(() => document.AcknowledgeKeyFact(0, Now));
    }

    private static MeaningVerification Verification() =>
        MeaningVerification.Restore(
            VerificationStatus.Verified,
            [KeyFact.Create(KeyFactType.Amount, "250 €", null, new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation))],
            new VerificationMetadata("Proveedor", "modelo-verificador", "verify.v1"));

    private static AdaptedContent Content(string sentence) =>
        AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Qué es?", [sentence])], [])!;

    private static Document Adapted()
    {
        var document = Submit();
        document.StartProcessing(Now);
        document.CompleteAdaptation(Content("Es una ayuda."), Metadata, Report, Verification(), Now);
        return document;
    }

    private static Document Submit() => Document.Submit(
        OwnerId.From(Guid.NewGuid()),
        DocumentTitle.Create("Bases"),
        OriginalText.Create("Una ayuda de 250 € para el alquiler.", new TextLengthLimits(10, 100)),
        Now);
}
