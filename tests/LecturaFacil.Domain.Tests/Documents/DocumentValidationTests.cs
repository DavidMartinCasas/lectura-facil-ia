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
    public void RecordValidation_Approved_ValidatesTheLatestVersion()
    {
        var document = Pending();

        var record = document.RecordValidation(
            ValidationOutcome.Approved,
            "  Grupo de lectura   de la asociación ",
            "  Se entiende bien.\r\nCambiar una palabra.  ",
            Now.AddDays(2));

        document.Status.ShouldBe(DocumentStatus.Validated);
        record.Number.ShouldBe(1);
        record.VersionNumber.ShouldBe(1);
        record.ValidatedBy.ShouldBe("Grupo de lectura de la asociación");
        record.Notes.ShouldBe("Se entiende bien.\nCambiar una palabra.");
        record.RecordedAt.ShouldBe(Now.AddDays(2));
        document.LatestValidation.ShouldBe(record);
    }

    [Fact]
    public void RecordValidation_ChangesRequested_ReturnsItToDraftToFixIt()
    {
        var document = Pending();

        var record = document.RecordValidation(ValidationOutcome.ChangesRequested, "Grupo", "   ", Now.AddDays(1));

        document.Status.ShouldBe(DocumentStatus.Draft);
        document.IsEditable.ShouldBeTrue();
        record.Notes.ShouldBeNull();
    }

    [Fact]
    public void RecordValidation_KeepsEveryRoundInOrder()
    {
        var document = Pending();
        document.RecordValidation(ValidationOutcome.ChangesRequested, "Grupo A", null, Now.AddDays(1));
        document.SendToValidation(Now.AddDays(2));

        document.RecordValidation(ValidationOutcome.Approved, "Grupo B", null, Now.AddDays(3));

        document.Validations.Select(validation => (validation.Number, validation.ValidatedBy)).ShouldBe([(1, "Grupo A"), (2, "Grupo B")]);
        document.LatestValidation!.Outcome.ShouldBe(ValidationOutcome.Approved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordValidation_WithoutWhoValidatedIt_Throws(string validatedBy)
    {
        var document = Pending();

        Should.Throw<ArgumentException>(() => document.RecordValidation(ValidationOutcome.Approved, validatedBy, null, Now));
        document.Status.ShouldBe(DocumentStatus.PendingValidation);
        document.Validations.ShouldBeEmpty();
    }

    [Fact]
    public void RecordValidation_WithTooLongTexts_Throws()
    {
        var document = Pending();

        Should.Throw<ArgumentException>(() => document.RecordValidation(
            ValidationOutcome.Approved, new string('a', ValidationRecord.ValidatedByMaxLength + 1), null, Now));
        Should.Throw<ArgumentException>(() => document.RecordValidation(
            ValidationOutcome.Approved, "Grupo", new string('a', ValidationRecord.NotesMaxLength + 1), Now));
    }

    [Fact]
    public void RecordValidation_WhenItWasNotSent_Throws() =>
        Should.Throw<InvalidOperationException>(() => Reviewed().RecordValidation(ValidationOutcome.Approved, "Grupo", null, Now));

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

        document.RecordValidation(ValidationOutcome.Approved, "Grupo", null, Now.AddDays(1));
        document.Publish(Now.AddDays(2));

        document.Status.ShouldBe(DocumentStatus.Published);
        document.IsEditable.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => document.Publish(Now));
    }

    private static Document Pending()
    {
        var document = Reviewed();
        document.SendToValidation(Now);
        return document;
    }

    private static Document Reviewed()
    {
        var document = Adapted();
        document.AcknowledgeAllKeyFacts(Now);
        return document;
    }

    private static Document Adapted()
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
            new VerificationMetadata("Proveedor", "modelo-verificador", "verify.v1"));
        var content = AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Qué es?", ["Es una ayuda."])], [])!;
        document.CompleteAdaptation(content, Metadata, Report, verification, Now);
        return document;
    }
}
