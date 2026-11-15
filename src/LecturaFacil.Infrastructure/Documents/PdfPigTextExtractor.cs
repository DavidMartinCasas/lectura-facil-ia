// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;

namespace LecturaFacil.Infrastructure.Documents;

/// <summary>
/// Extrae el texto de un PDF digital con PdfPig, en memoria. Contra PDFs que agotan CPU o memoria: límite de páginas
/// y tiempo máximo (<see cref="LimitsOptions.MaxPdfPages"/>, <see cref="LimitsOptions.PdfExtractionTimeoutSeconds"/>).
/// Nunca registra el texto: solo tamaño, páginas y tiempo.
/// </summary>
public sealed partial class PdfPigTextExtractor(
    IOptions<LimitsOptions> limits,
    TimeProvider timeProvider,
    ILogger<PdfPigTextExtractor> logger) : ITextExtractor
{
    private const string ParagraphSeparator = "\n\n";

    private readonly LimitsOptions _limits = limits.Value;

    public async Task<Result<ExtractedText>> ExtractTextAsync(byte[] pdf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        cancellationToken.ThrowIfCancellationRequested();

        var timeout = TimeSpan.FromSeconds(_limits.PdfExtractionTimeoutSeconds);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var started = timeProvider.GetTimestamp();

        Result<ExtractedText> result;
        try
        {
            result = await Task.Run(() => Extract(pdf, started, timeout, deadline.Token), deadline.Token)
                .WaitAsync(timeout, timeProvider, cancellationToken);
        }
        catch (TimeoutException)
        {
            result = Result<ExtractedText>.Failure(DocumentErrors.PdfTooComplex);
        }
        finally
        {
            // Si se ha agotado el tiempo, la extracción en segundo plano se detiene en la siguiente página.
            await deadline.CancelAsync();
        }

        Log(pdf.Length, result, timeProvider.GetElapsedTime(started));
        return result;
    }

    private Result<ExtractedText> Extract(byte[] pdf, long started, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var document = PdfDocument.Open(pdf);
            if (document.NumberOfPages > _limits.MaxPdfPages)
            {
                return Result<ExtractedText>.Failure(DocumentErrors.PdfTooComplex);
            }

            var pages = new List<string>(document.NumberOfPages);
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (timeProvider.GetElapsedTime(started) > timeout)
                {
                    return Result<ExtractedText>.Failure(DocumentErrors.PdfTooComplex);
                }

                pages.Add(ReadParagraphs(page));
            }

            var text = string.Join(ParagraphSeparator, pages.Where(page => page.Length > 0));
            return string.IsNullOrWhiteSpace(text)
                ? Result<ExtractedText>.Failure(DocumentErrors.ScannedPdf)
                : Result<ExtractedText>.Success(new ExtractedText(text, document.NumberOfPages));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // PdfPig lanza tipos muy distintos ante un PDF dañado o cifrado. El mensaje puede incluir contenido: no se registra.
            return Result<ExtractedText>.Failure(DocumentErrors.UnreadablePdf);
        }
    }

    /// <summary>Un párrafo por bloque de texto, en orden de lectura; las líneas de un bloque se unen con un espacio.</summary>
    private static string ReadParagraphs(Page page)
    {
        var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(page.GetWords());
        var paragraphs = UnsupervisedReadingOrderDetector.Instance.Get(blocks)
            .Select(block => string.Join(' ', block.TextLines.Select(line => line.Text.Trim())).Trim())
            .Where(paragraph => paragraph.Length > 0);
        return string.Join(ParagraphSeparator, paragraphs);
    }

    private void Log(int bytes, Result<ExtractedText> result, TimeSpan elapsed)
    {
        if (result.IsSuccess)
        {
            PdfRead(logger, bytes, result.Value.PageCount, (long)elapsed.TotalMilliseconds);
        }
        else
        {
            PdfRejected(logger, bytes, result.Error!.Code, (long)elapsed.TotalMilliseconds);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "PDF leído: {Bytes} bytes, {Pages} páginas, {ElapsedMs} ms.")]
    private static partial void PdfRead(ILogger logger, int bytes, int pages, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "PDF rechazado: {Bytes} bytes, {ErrorCode}, {ElapsedMs} ms.")]
    private static partial void PdfRejected(ILogger logger, int bytes, string errorCode, long elapsedMs);
}
