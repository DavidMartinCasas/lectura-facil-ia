// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Infrastructure.Documents;
using LecturaFacil.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace LecturaFacil.Infrastructure.Tests.Documents;

public sealed class PdfPigTextExtractorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExtractText_WhenPdfIsDigital_ReturnsTheTextWithParagraphsSeparatedByABlankLine()
    {
        var result = await CreateExtractor().ExtractTextAsync(PdfFixtures.Read(PdfFixtures.Normal), Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PageCount.ShouldBe(2);
        result.Value.Text.ShouldBe(
            "Convocatoria de ayudas de ejemplo para el curso escolar. "
            + "Este documento es ficticio y solo sirve para las pruebas automaticas.\n\n"
            + "Las personas interesadas pueden presentar la solicitud hasta el 30 de junio. "
            + "La solicitud se presenta en la sede electronica del organismo de ejemplo.\n\n"
            + "Segunda pagina del documento de prueba. "
            + "La cuantia maxima de la ayuda es de 300 euros por persona.");
    }

    [Fact]
    public async Task ExtractText_WhenPagesHaveNoText_ReturnsScannedPdf()
    {
        var result = await CreateExtractor().ExtractTextAsync(PdfFixtures.Read(PdfFixtures.WithoutText), Token);

        result.Error.ShouldBe(DocumentErrors.ScannedPdf);
    }

    [Fact]
    public async Task ExtractText_WhenPdfIsCorrupt_ReturnsUnreadablePdf()
    {
        var result = await CreateExtractor().ExtractTextAsync(PdfFixtures.Read(PdfFixtures.Corrupt), Token);

        result.Error.ShouldBe(DocumentErrors.UnreadablePdf);
    }

    [Fact]
    public async Task ExtractText_WhenPdfHasMorePagesThanTheLimit_ReturnsPdfTooComplexWithoutReadingThem()
    {
        var result = await CreateExtractor(maxPages: 2).ExtractTextAsync(BuildPdf(pages: 3), Token);

        result.Error.ShouldBe(DocumentErrors.PdfTooComplex);
    }

    [Fact]
    public async Task ExtractText_WhenPdfHasExactlyTheMaximumPages_ReadsThem()
    {
        var result = await CreateExtractor(maxPages: 3).ExtractTextAsync(BuildPdf(pages: 3), Token);

        result.Value.PageCount.ShouldBe(3);
        result.Value.Text.ShouldBe("Pagina 1 de prueba.\n\nPagina 2 de prueba.\n\nPagina 3 de prueba.");
    }

    [Fact]
    public async Task ExtractText_WhenReadingTakesLongerThanTheTimeout_ReturnsPdfTooComplex()
    {
        var timeProvider = new ExpiredTimeProvider();
        var extractor = CreateExtractor(timeProvider: timeProvider);

        var result = await extractor.ExtractTextAsync(BuildPdf(pages: 2), Token);

        result.Error.ShouldBe(DocumentErrors.PdfTooComplex);
    }

    [Fact]
    public async Task ExtractText_WhenPagesHaveMoreLettersThanMaxCharacters_ReturnsTextTooLongBeforeReadingParagraphs()
    {
        var result = await CreateExtractor(maxCharacters: 20).ExtractTextAsync(BuildPdf(pages: 2), Token);

        result.Error!.Code.ShouldBe(DocumentErrors.TextTooLongCode);
    }

    [Fact]
    public async Task ExtractText_WhenCalledManyTimesAtOnce_ReleasesEverySlot()
    {
        using var extractor = CreateExtractor(maxConcurrent: 1);
        var pdf = PdfFixtures.Read(PdfFixtures.Normal);

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => extractor.ExtractTextAsync(pdf, Token)));

        results.ShouldAllBe(result => result.IsSuccess);
    }

    [Fact]
    public async Task ExtractText_WhenRequestIsCancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => CreateExtractor().ExtractTextAsync(PdfFixtures.Read(PdfFixtures.Normal), cancellation.Token));
    }

    private static PdfPigTextExtractor CreateExtractor(
        int maxPages = 100,
        TimeProvider? timeProvider = null,
        int maxCharacters = 20_000,
        int maxConcurrent = 2) =>
        new(
            Options.Create(new LimitsOptions
            {
                MaxPdfPages = maxPages,
                PdfExtractionTimeoutSeconds = 5,
                MaxCharacters = maxCharacters,
                MaxConcurrentPdfExtractions = maxConcurrent,
            }),
            timeProvider ?? TimeProvider.System,
            NullLogger<PdfPigTextExtractor>.Instance);

    private static byte[] BuildPdf(int pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (var number = 1; number <= pages; number++)
        {
            builder.AddPage(PageSize.A4).AddText($"Pagina {number} de prueba.", 12, new PdfPoint(60, 760), font);
        }

        return builder.Build();
    }

    /// <summary>Cada consulta del reloj avanza una hora: cualquier plazo vence en cuanto se comprueba.</summary>
    private sealed class ExpiredTimeProvider : TimeProvider
    {
        private long _ticks = DateTimeOffset.UnixEpoch.UtcTicks;

        public override long GetTimestamp() => Interlocked.Add(ref _ticks, TimeSpan.TicksPerHour);

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    }
}
