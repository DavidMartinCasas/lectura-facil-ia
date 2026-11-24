// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Domain.Tests.Documents;

/// <summary>Validación humana de un borrador: enviarlo, anotar lo que decide el grupo y publicarlo.</summary>
public sealed class DocumentValidationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly AdaptationMetadata Metadata = new("Proveedor", "modelo", "adapter.v1", 100, 50, TimeSpan.FromSeconds(2));
    private static readonly GuidelineReport Report = GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10);
    private static readonly VerificationMetadata VerifierMetadata = new("Proveedor", "modelo-verificador", "verify.v1");

    [Fact]
    public void SendToValidation_WhenTheWarningsAreReviewed_LeavesItPendingAndNoLongerEditable()
    {
        var document = Reviewed();

        document.CanSendToValidation.ShouldBeTrue();
        document.SendToValidation(Now.AddHours(1));

        document.Status.ShouldBe(DocumentStatus.PendingValidation);
        document.IsEditable.ShouldBeFalse();
        document.UpdatedAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void SendToValidation_WithPendingKeyFacts_Throws()
    {
        var document = Adapted();

        document.CanSendToValidation.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => document.SendToValidation(Now));
        document.Status.ShouldBe(DocumentStatus.Draft);
    }

    [Fact]
    public void SendToValidation_WhenItIsNotADraft_Throws()
    {
        var document = Pending();

        document.CanSendToValidation.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => document.SendToValidation(Now));
    }

    [Fact]
    public void SendToValidation_AfterTheGroupAskedForChanges_WaitsForANewVersion()
    {
        var document = Pending();
        document.RecordValidation(Decision(ValidationOutcome.ChangesRequested), Now.AddDays(1));

        document.AwaitsChanges.ShouldBeTrue();
        document.CanSendToValidation.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => document.SendToValidation(Now.AddDays(1)));

        Edit(document, Now.AddDays(2));

        document.AwaitsChanges.ShouldBeFalse();
        document.CanSendToValidation.ShouldBeTrue();
    }

    [Fact]
    public void SendToValidation_AfterWithdrawingIt_DoesNotWaitForChanges()
    {
        var document = Pending();
        document.WithdrawFromValidation(Now.AddHours(1));

        document.AwaitsChanges.ShouldBeFalse();
        document.CanSendToValidation.ShouldBeTrue();
    }

    [Fact]
    public void RecordValidation_Approved_ValidatesTheLatestVersion()
    {
        var document = Pending();

        var record = document.RecordValidation(
            Decision(
                ValidationOutcome.Approved,
                "  Grupo de lectura   de la asociación ",
                "  Se entiende bien.\r\nCambiar una palabra.  ",
                participants: 5),
            Now.AddDays(2));

        document.Status.ShouldBe(DocumentStatus.Validated);
        record.Number.ShouldBe(1);
        record.VersionNumber.ShouldBe(1);
        record.ValidatedBy.ShouldBe("Grupo de lectura de la asociación");
        record.Participants.ShouldBe(5);
        record.HardSections.ShouldBeEmpty();
        record.HardGlossary.ShouldBeFalse();
        record.Notes.ShouldBe("Se entiende bien.\nCambiar una palabra.");
        record.RecordedAt.ShouldBe(Now.AddDays(2));
        document.LatestValidation.ShouldBe(record);
    }

    [Fact]
    public void RecordValidation_ChangesRequested_ReturnsItToDraftToFixIt()
    {
        var document = Pending();

        var record = document.RecordValidation(Decision(ValidationOutcome.ChangesRequested, notes: "   "), Now.AddDays(1));

        document.Status.ShouldBe(DocumentStatus.Draft);
        document.IsEditable.ShouldBeTrue();
        record.Notes.ShouldBeNull();
    }

    [Fact]
    public void RecordValidation_ChangesRequested_KeepsTheQuestionsOfTheHardSectionsInOrder()
    {
        var document = Pending();

        var record = document.RecordValidation(
            Decision(ValidationOutcome.ChangesRequested, sections: [1, 0], glossary: true),
            Now.AddDays(1));

        record.HardSections.ShouldBe(["¿Qué es?", "¿Cuánto dan?"]);
        record.HardGlossary.ShouldBeTrue();

        Edit(document, Now.AddDays(2));
        document.LatestValidation!.HardSections.ShouldBe(["¿Qué es?", "¿Cuánto dan?"]);
    }

    [Theory]
    [InlineData(ValidationOutcome.Approved, new[] { 0 }, false)]
    [InlineData(ValidationOutcome.Approved, new int[0], true)]
    [InlineData(ValidationOutcome.ChangesRequested, new[] { 2 }, false)]
    [InlineData(ValidationOutcome.ChangesRequested, new[] { -1 }, false)]
    [InlineData(ValidationOutcome.ChangesRequested, new[] { 0, 0 }, false)]
    public void RecordValidation_WithHardPartsThatAreNotAllowed_Throws(ValidationOutcome outcome, int[] sections, bool glossary)
    {
        var document = Pending();

        Should.Throw<ArgumentException>(() => document.RecordValidation(Decision(outcome, sections: sections, glossary: glossary), Now));
        document.Validations.ShouldBeEmpty();
    }

    [Fact]
    public void RecordValidation_WithAHardGlossaryInAVersionWithoutOne_Throws()
    {
        var document = Pending(withGlossary: false);

        Should.Throw<ArgumentException>(() => document.RecordValidation(
            Decision(ValidationOutcome.ChangesRequested, glossary: true), Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ValidationRecord.MaxParticipants + 1)]
    public void RecordValidation_WithAnImpossibleNumberOfPeople_Throws(int participants)
    {
        var document = Pending();

        Should.Throw<ArgumentOutOfRangeException>(() => document.RecordValidation(
            Decision(ValidationOutcome.Approved, participants: participants), Now));
    }

    [Fact]
    public void RecordValidation_KeepsEveryRoundInOrder()
    {
        var document = Pending();
        document.RecordValidation(Decision(ValidationOutcome.ChangesRequested, "Grupo A"), Now.AddDays(1));
        Edit(document, Now.AddDays(2));
        document.SendToValidation(Now.AddDays(2));

        document.RecordValidation(Decision(ValidationOutcome.Approved, "Grupo B"), Now.AddDays(3));

        document.Validations.Select(validation => (validation.Number, validation.VersionNumber, validation.ValidatedBy))
            .ShouldBe([(1, 1, "Grupo A"), (2, 2, "Grupo B")]);
        document.LatestValidation!.Outcome.ShouldBe(ValidationOutcome.Approved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordValidation_WithoutWhoValidatedIt_Throws(string validatedBy)
    {
        var document = Pending();

        Should.Throw<ArgumentException>(() => document.RecordValidation(Decision(ValidationOutcome.Approved, validatedBy), Now));
        document.Status.ShouldBe(DocumentStatus.PendingValidation);
        document.Validations.ShouldBeEmpty();
    }

    [Fact]
    public void RecordValidation_WithTooLongTexts_Throws()
    {
        var document = Pending();

        Should.Throw<ArgumentException>(() => document.RecordValidation(
            Decision(ValidationOutcome.Approved, new string('a', ValidationRecord.ValidatedByMaxLength + 1)), Now));
        Should.Throw<ArgumentException>(() => document.RecordValidation(
            Decision(ValidationOutcome.Approved, notes: new string('a', ValidationRecord.NotesMaxLength + 1)), Now));
    }

    [Fact]
    public void RecordValidation_WhenItWasNotSent_Throws() =>
        Should.Throw<InvalidOperationException>(() => Reviewed().RecordValidation(Decision(ValidationOutcome.Approved), Now));

    [Fact]
    public void WithdrawFromValidation_ReturnsItToDraftWithoutARecord()
    {
        var document = Pending();

        document.WithdrawFromValidation(Now.AddHours(2));

        document.Status.ShouldBe(DocumentStatus.Draft);
        document.Validations.ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(() => document.WithdrawFromValidation(Now));
    }

    [Fact]
    public void Publish_OnlyAValidatedDocument()
    {
        var document = Pending();
        Should.Throw<InvalidOperationException>(() => document.Publish(Now));

        document.RecordValidation(Decision(ValidationOutcome.Approved), Now.AddDays(1));
        document.Publish(Now.AddDays(2));

        document.Status.ShouldBe(DocumentStatus.Published);
        document.IsEditable.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => document.Publish(Now));
    }

    private static GroupDecision Decision(
        ValidationOutcome outcome,
        string validatedBy = "Grupo",
        string? notes = null,
        int participants = 4,
        int[]? sections = null,
        bool glossary = false) =>
        new(outcome, validatedBy, participants, sections ?? [], glossary, notes);

    private static void Edit(Document document, DateTimeOffset now)
    {
        var content = AdaptedContent.TryCreate(
            "Ayudas",
            [new AdaptedSectionDraft("¿Qué es esta ayuda?", ["Es dinero para el alquiler."])],
            [])!;
        document.SaveManualEdit(content, Metadata, Report, MeaningVerification.Restore(VerificationStatus.Verified, [], VerifierMetadata), now);
    }

    private static Document Pending(bool withGlossary = true)
    {
        var document = Reviewed(withGlossary);
        document.SendToValidation(Now);
        return document;
    }

    private static Document Reviewed(bool withGlossary = true)
    {
        var document = Adapted(withGlossary);
        document.AcknowledgeAllKeyFacts(Now);
        return document;
    }

    private static Document Adapted(bool withGlossary = true)
    {
        var document = Document.Submit(
            OwnerId.From(Guid.NewGuid()),
            DocumentTitle.Create("Bases"),
            OriginalText.Create("Una ayuda de 250 € para el alquiler.", new TextLengthLimits(10, 100)),
            Now);
        document.StartProcessing(Now);
        var verification = MeaningVerification.Restore(
            VerificationStatus.Verified,
            [KeyFact.Create(KeyFactType.Amount, "250 €", null, new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation))],
            VerifierMetadata);
        var content = AdaptedContent.TryCreate(
            "Ayudas",
            [new AdaptedSectionDraft("¿Qué es?", ["Es una ayuda."]), new AdaptedSectionDraft("¿Cuánto dan?", ["Dan 250 €."])],
            withGlossary ? [new GlossaryTermDraft("Alquiler", "Dinero que pagas por vivir en una casa.")] : [])!;
        document.CompleteAdaptation(content, Metadata, Report, verification, Now);
        return document;
    }
}
