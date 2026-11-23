// SPDX-License-Identifier: Apache-2.0
using System.Text;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Export;
using LecturaFacil.Application.Tests.Documents;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Application.Tests.Export;

public sealed class ExportDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = OwnerId.From(Guid.NewGuid());

    private readonly InMemoryDocumentRepository _repository = new();
    private readonly RecordingExporter _exporter = new(ExportFormat.PlainText, "text/plain; charset=utf-8", "txt");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Export_WhenAllKeyFactsAreReviewed_ReturnsTheFileFromTheChosenExporter()
    {
        var document = Adapted(Owner, pendingWarning: false);

        var result = await CreateUseCase().ExecuteAsync(document.Id, "txt", Token);

        var file = result.Value;
        file.ContentType.ShouldBe("text/plain; charset=utf-8");
        file.FileName.ShouldBe("ayudas-al-alquiler.txt");
        Encoding.UTF8.GetString(file.Content).ShouldBe("Ayudas al alquiler");
    }

    [Fact]
    public async Task Export_BuildsTheIntermediateModelFromTheLatestVersion()
    {
        var document = Adapted(Owner, pendingWarning: false);

        await CreateUseCase().ExecuteAsync(document.Id, "txt", Token);

        var exported = _exporter.Exported.ShouldHaveSingleItem();
        exported.Title.ShouldBe("Ayudas al alquiler");
        exported.Language.ShouldBe("es");
        var section = exported.Sections.ShouldHaveSingleItem();
        section.Question.ShouldBe("¿Qué es?");
        section.Sentences.ShouldBe(["Una ayuda de 250 €.", "Es para pagar el alquiler."]);
        exported.GlossaryTitle.ShouldBe("Glosario");
        exported.Glossary.ShouldHaveSingleItem().ShouldBe(new GlossaryTerm("Alquiler", "Dinero que pagas cada mes por vivir en una casa."));
    }

    [Fact]
    public async Task Export_AddsTheDraftNoticeAndTheDateToTheFooter()
    {
        var document = Adapted(Owner, pendingWarning: false);

        await CreateUseCase().ExecuteAsync(document.Id, "txt", Token);

        _exporter.Exported.ShouldHaveSingleItem().Footer.ShouldBe("Borrador · pendiente de validación · 16 de noviembre de 2026");
    }

    [Fact]
    public async Task Export_OfAValidatedDocument_SaysItIsTheValidatedVersion()
    {
        var document = Adapted(Owner, pendingWarning: false);
        document.SendToValidation(Now);
        document.RecordValidation(ValidationOutcome.Approved, "Grupo de lectura", null, Now);

        await CreateUseCase().ExecuteAsync(document.Id, "txt", Token);

        var exported = _exporter.Exported.ShouldHaveSingleItem();
        exported.Cover.DraftNotice.ShouldBe("Versión validada");
        exported.Footer.ShouldBe("Versión validada · 16 de noviembre de 2026");
    }

    [Fact]
    public async Task Export_PreparesACoverWithWhatItIsTheDraftNoticeAndTheDate()
    {
        var document = Adapted(Owner, pendingWarning: false);

        await CreateUseCase().ExecuteAsync(document.Id, "txt", Token);

        _exporter.Exported.ShouldHaveSingleItem().Cover.ShouldBe(
            new ExportCover("Adaptación a lectura fácil", "Borrador · pendiente de validación", "16 de noviembre de 2026"));
    }

    [Fact]
    public async Task Export_WhenAKeyFactWarningIsPending_ReturnsPendingWarnings()
    {
        var document = Adapted(Owner, pendingWarning: true);

        var result = await CreateUseCase().ExecuteAsync(document.Id, "txt", Token);

        result.Error.ShouldBe(ExportErrors.PendingWarnings);
        result.Error!.Message.ShouldBe("Revisa los avisos antes de exportar");
        _exporter.Exported.ShouldBeEmpty();
    }

    [Fact]
    public async Task Export_WhenTheWarningHasBeenAcknowledged_Succeeds()
    {
        var document = Adapted(Owner, pendingWarning: true);
        document.AcknowledgeKeyFact(0, Now);

        var result = await CreateUseCase().ExecuteAsync(document.Id, "txt", Token);

        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("exe")]
    [InlineData("")]
    [InlineData("docx ")]
    [InlineData("../txt")]
    public async Task Export_WhenTheFormatIsNotInTheAllowList_ReturnsUnsupportedFormat(string format)
    {
        var document = Adapted(Owner, pendingWarning: false);

        var result = await CreateUseCase().ExecuteAsync(document.Id, format, Token);

        result.Error.ShouldBe(ExportErrors.UnsupportedFormat);
        _exporter.Exported.ShouldBeEmpty();
    }

    [Fact]
    public async Task Export_WhenTheFormatIsAllowedButHasNoExporter_ReturnsUnsupportedFormat()
    {
        var document = Adapted(Owner, pendingWarning: false);

        var result = await CreateUseCase().ExecuteAsync(document.Id, "pdf", Token);

        result.Error.ShouldBe(ExportErrors.UnsupportedFormat);
    }

    [Fact]
    public async Task Export_WhenTheDocumentBelongsToSomeoneElse_ReturnsNotFound()
    {
        var document = Adapted(OwnerId.From(Guid.NewGuid()), pendingWarning: false);

        var result = await CreateUseCase().ExecuteAsync(document.Id, "txt", Token);

        result.Error.ShouldBe(DocumentErrors.NotFound);
        _exporter.Exported.ShouldBeEmpty();
    }

    [Fact]
    public async Task Export_WhenTheDocumentDoesNotExist_ReturnsNotFound()
    {
        var result = await CreateUseCase().ExecuteAsync(DocumentId.New(), "txt", Token);

        result.Error.ShouldBe(DocumentErrors.NotFound);
    }

    [Fact]
    public async Task Export_WhenThereIsNoAdaptationYet_ReturnsNothingToExport()
    {
        var document = Submit(Owner);

        var result = await CreateUseCase().ExecuteAsync(document.Id, "txt", Token);

        result.Error.ShouldBe(ExportErrors.NothingToExport);
    }

    [Fact]
    public void Constructor_WhenTwoExportersShareAFormat_Throws() =>
        Should.Throw<ArgumentException>(() => new ExportDocument(
            _repository,
            new FixedCurrentUser(Owner),
            [_exporter, new RecordingExporter(ExportFormat.PlainText, "text/plain", "txt")],
            new FixedTimeProvider(Now)));

    private Document Adapted(OwnerId owner, bool pendingWarning)
    {
        var document = Submit(owner);
        document.StartProcessing(Now);
        var verdict = pendingWarning ? new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation) : KeyFactVerdict.Conserved;
        document.CompleteAdaptation(
            AdaptedContent.TryCreate(
                "Ayudas al alquiler",
                [new AdaptedSectionDraft("¿Qué es?", ["Una ayuda de 250 €.", "Es para pagar el alquiler."])],
                [new GlossaryTermDraft("Alquiler", "Dinero que pagas cada mes por vivir en una casa.")])!,
            new AdaptationMetadata("Servidor propio", "modelo", "adapter.v1", 1, 1, TimeSpan.FromSeconds(1)),
            GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10),
            MeaningVerification.Restore(
                VerificationStatus.Verified,
                [KeyFact.Create(KeyFactType.Amount, "250 €", pendingWarning ? null : "250 €", verdict)],
                null),
            Now);
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

    private ExportDocument CreateUseCase() =>
        new(_repository, new FixedCurrentUser(Owner), [_exporter], new FixedTimeProvider(Now));

    private sealed class RecordingExporter(ExportFormat format, string contentType, string fileExtension) : IDocumentExporter
    {
        public List<ExportableDocument> Exported { get; } = [];

        public ExportFormat Format { get; } = format;

        public string ContentType { get; } = contentType;

        public string FileExtension { get; } = fileExtension;

        public byte[] Export(ExportableDocument document)
        {
            Exported.Add(document);
            return Encoding.UTF8.GetBytes(document.Title);
        }
    }
}
