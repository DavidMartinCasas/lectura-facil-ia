// SPDX-License-Identifier: Apache-2.0
using System.Text;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Tests.Documents;

public sealed class SubmitDocumentTests
{
    private const string ValidText = "Texto de prueba suficientemente largo para pasar la validación mínima de caracteres.";

    private static readonly OwnerId Owner = OwnerId.From(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryDocumentRepository _repository = new();
    private readonly RecordingAdaptationQueue _queue = new();
    private StubGuidelineChecker _guidelineChecker = new();
    private readonly LimitsOptions _limits = new() { MinCharacters = 50, MaxCharacters = 200, MaxFileSizeBytes = 2_048 };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SubmitText_WhenTextIsValid_StoresAReceivedDocumentOfTheCurrentUser()
    {
        var result = await CreateUseCase().SubmitTextAsync(new SubmitTextCommand(ValidText, "  Prueba\u0000 "), Token);

        result.IsSuccess.ShouldBeTrue();
        var document = _repository.Added.ShouldHaveSingleItem();
        document.Id.ShouldBe(result.Value.Id);
        document.OwnerId.ShouldBe(Owner);
        document.Status.ShouldBe(DocumentStatus.Received);
        document.Title.Value.ShouldBe("Prueba");
        document.OriginalText.Value.ShouldBe(ValidText);
        document.CreatedAt.ShouldBe(Now);
        result.Value.Status.ShouldBe(DocumentStatus.Received);
        result.Value.Characters.ShouldBe(ValidText.Length);
        result.Value.PageCount.ShouldBeNull();
    }

    [Fact]
    public async Task SubmitText_WhenTextIsValid_QueuesTheDocumentForAdaptation()
    {
        var result = await CreateUseCase().SubmitTextAsync(new SubmitTextCommand(ValidText, null), Token);

        _queue.Enqueued.ShouldBe([result.Value.Id]);
    }

    [Fact]
    public async Task SubmitText_WhenTextIsTooShort_DoesNotQueueAnything()
    {
        await CreateUseCase().SubmitTextAsync(new SubmitTextCommand("corto", null), Token);

        _queue.Enqueued.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitText_WhenTextIsValid_StoresTheFindingsOfTheNormalizedOriginalText()
    {
        var result = await CreateUseCase().SubmitTextAsync(new SubmitTextCommand("  " + ValidText + "\r\n", null), Token);

        result.IsSuccess.ShouldBeTrue();
        _guidelineChecker.CheckedTexts.ShouldBe([ValidText]);
        var finding = _repository.Added.ShouldHaveSingleItem().OriginalFindings.ShouldHaveSingleItem();
        finding.GuidelineCode.ShouldBe(GuidelineCodes.ShortSentences);
        finding.Fragment.ShouldBe(ValidText[..5]);
    }

    [Fact]
    public async Task SubmitPdf_WhenPdfIsValid_StoresTheFindingsOfItsText()
    {
        var result = await CreateUseCase().SubmitPdfAsync(PdfCommand("bases.pdf"), Token);

        result.IsSuccess.ShouldBeTrue();
        _repository.Added.ShouldHaveSingleItem().OriginalFindings.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SubmitText_WhenThereAreMoreFindingsThanTheMaximum_StoresOnlyTheMaximum()
    {
        _limits.MaxFindingsPerDocument = 10;
        _guidelineChecker = new StubGuidelineChecker(findingsPerText: 25);

        await CreateUseCase().SubmitTextAsync(new SubmitTextCommand(ValidText, null), Token);

        _repository.Added.ShouldHaveSingleItem().OriginalFindings.Count.ShouldBe(10);
    }

    [Fact]
    public async Task SubmitText_WhenTextIsTooLong_DoesNotAnalyzeIt()
    {
        await CreateUseCase().SubmitTextAsync(new SubmitTextCommand(new string('a', 201), null), Token);

        _guidelineChecker.CheckedTexts.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitText_WhenTitleIsMissing_UsesTheDefaultTitle()
    {
        await CreateUseCase().SubmitTextAsync(new SubmitTextCommand(ValidText, null), Token);

        _repository.Added.ShouldHaveSingleItem().Title.ShouldBe(DocumentTitle.Default);
    }

    [Fact]
    public async Task SubmitText_WhenTextIsTooShortAfterNormalizing_ReturnsTextTooShortAndStoresNothing()
    {
        var text = "Corto" + new string('\u0000', 60);

        var result = await CreateUseCase().SubmitTextAsync(new SubmitTextCommand(text, null), Token);

        result.Error!.Code.ShouldBe(DocumentErrors.TextTooShortCode);
        result.Error.Message.ShouldContain("50");
        _repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitText_WhenTheTextNeedsMorePartsThanAllowed_ReturnsTextTooLongBeforeQueueingIt()
    {
        _limits.MaxCharactersPerAdaptationChunk = 60;
        _limits.MaxAdaptationChunksPerDocument = 2;
        var text = string.Join("\n\n", Enumerable.Repeat(ValidText[..55], 3));

        var result = await CreateUseCase().SubmitTextAsync(new SubmitTextCommand(text, null), Token);

        result.Error!.Code.ShouldBe(DocumentErrors.TextTooLongCode);
        result.Error.Message.ShouldContain("más de 2 partes");
        _repository.Added.ShouldBeEmpty();
        _queue.Enqueued.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitText_WhenTextIsTooLong_ReturnsTextTooLongAndStoresNothing()
    {
        var result = await CreateUseCase().SubmitTextAsync(new SubmitTextCommand(new string('a', 201), null), Token);

        result.Error!.Code.ShouldBe(DocumentErrors.TextTooLongCode);
        result.Error.Message.ShouldContain("200");
        _repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitPdf_WhenPdfHasText_StoresTheExtractedTextWithTheFileNameAsTitle()
    {
        var extractor = StubTextExtractor.Returning("Primer párrafo del documento de prueba.\n\nSegundo párrafo del documento.", pages: 2);

        var result = await CreateUseCase(extractor).SubmitPdfAsync(PdfCommand("../Bases ayudas.pdf"), Token);

        result.IsSuccess.ShouldBeTrue();
        var document = _repository.Added.ShouldHaveSingleItem();
        document.Title.Value.ShouldBe("Bases ayudas");
        document.OriginalText.Value.ShouldBe("Primer párrafo del documento de prueba.\n\nSegundo párrafo del documento.");
        document.Status.ShouldBe(DocumentStatus.Received);
        result.Value.PageCount.ShouldBe(2);
    }

    [Fact]
    public async Task SubmitPdf_WhenDeclaredLengthIsAboveTheLimit_ReturnsFileTooLargeWithoutReadingIt()
    {
        var extractor = StubTextExtractor.Returning(ValidText);
        using var content = new LyingLengthStream(PdfBytes(), _limits.MaxFileSizeBytes + 1L);

        var result = await CreateUseCase(extractor).SubmitPdfAsync(new SubmitPdfCommand(content, "a.pdf"), Token);

        result.Error.ShouldBe(DocumentErrors.FileTooLarge(_limits.MaxFileSizeBytes));
        content.Position.ShouldBe(0);
        extractor.Calls.ShouldBe(0);
        _repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitPdf_WhenRealContentIsAboveTheLimit_ReturnsFileTooLarge()
    {
        var extractor = StubTextExtractor.Returning(ValidText);
        using var content = new LyingLengthStream(PdfBytes(_limits.MaxFileSizeBytes + 10), reportedLength: 10);

        var result = await CreateUseCase(extractor).SubmitPdfAsync(new SubmitPdfCommand(content, "a.pdf"), Token);

        result.Error!.Code.ShouldBe(DocumentErrors.FileTooLargeCode);
        result.Error.Message.ShouldContain("2 KB");
        extractor.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task SubmitPdf_WhenSignatureIsNotPdf_ReturnsInvalidFileTypeAndNeverExtracts()
    {
        var extractor = StubTextExtractor.Returning(ValidText);
        using var content = new MemoryStream(Encoding.ASCII.GetBytes("PK\u0003\u0004 esto es un zip con extensión .pdf"));

        var result = await CreateUseCase(extractor).SubmitPdfAsync(new SubmitPdfCommand(content, "documento.pdf"), Token);

        result.Error.ShouldBe(DocumentErrors.InvalidFileType);
        extractor.Calls.ShouldBe(0);
        _repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitPdf_WhenExtractorFails_ReturnsItsErrorAndStoresNothing()
    {
        var extractor = StubTextExtractor.Failing(DocumentErrors.ScannedPdf);

        var result = await CreateUseCase(extractor).SubmitPdfAsync(PdfCommand("escaneado.pdf"), Token);

        result.Error.ShouldBe(DocumentErrors.ScannedPdf);
        result.Error!.Message.ShouldBe("Este PDF parece escaneado. De momento solo se admiten PDF con texto.");
        _repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitPdf_WhenExtractedTextIsShorterThanTheMinimum_ReturnsScannedPdf()
    {
        var extractor = StubTextExtractor.Returning("Página 1");

        var result = await CreateUseCase(extractor).SubmitPdfAsync(PdfCommand("escaneado-con-pie.pdf"), Token);

        result.Error.ShouldBe(DocumentErrors.ScannedPdf);
        _repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitPdf_WhenExtractedTextIsTooLong_ReturnsTextTooLong()
    {
        var extractor = StubTextExtractor.Returning(new string('a', 500));

        var result = await CreateUseCase(extractor).SubmitPdfAsync(PdfCommand("largo.pdf"), Token);

        result.Error!.Code.ShouldBe(DocumentErrors.TextTooLongCode);
        _repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitPdf_WhenCommandIsNull_Throws()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => CreateUseCase().SubmitPdfAsync(null!, Token));
        await Should.ThrowAsync<ArgumentNullException>(() => CreateUseCase().SubmitTextAsync(null!, Token));
    }

    [Fact]
    public void DocumentErrors_WhenFormattingSizes_UsesMegabytesForLargeLimitsAndKilobytesForSmallOnes()
    {
        DocumentErrors.FileTooLarge(5 * 1024 * 1024).Message.ShouldContain("5 MB");
        DocumentErrors.FileTooLarge(2_048).Message.ShouldContain("2 KB");
        DocumentErrors.FileTooLarge(5_767_168).Message.ShouldContain("5,5 MB");
    }

    private static SubmitPdfCommand PdfCommand(string fileName) => new(new MemoryStream(PdfBytes()), fileName);

    private static byte[] PdfBytes(int size = 64)
    {
        var bytes = new byte[size];
        Encoding.ASCII.GetBytes("%PDF-1.7\n").CopyTo(bytes, 0);
        return bytes;
    }

    private SubmitDocument CreateUseCase(ITextExtractor? extractor = null) =>
        new(
            extractor ?? StubTextExtractor.Returning(ValidText),
            _repository,
            new FixedCurrentUser(Owner),
            Options.Create(_limits),
            new FixedTimeProvider(Now),
            _guidelineChecker,
            _queue);
}
