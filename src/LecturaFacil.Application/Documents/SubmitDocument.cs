// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Documents;

/// <summary>
/// Recibe un texto pegado o un PDF digital y crea un documento en estado <see cref="DocumentStatus.Received"/>, con los
/// problemas de lectura fácil que tiene su texto original.
/// </summary>
public sealed class SubmitDocument(
    ITextExtractor textExtractor,
    IDocumentRepository repository,
    ICurrentUser currentUser,
    IOptions<LimitsOptions> limits,
    TimeProvider timeProvider,
    IGuidelineChecker guidelineChecker)
{
    // Tamaño del bloque de lectura: el mismo que usa Stream.CopyToAsync por defecto.
    private const int ReadBufferSize = 81_920;

    private readonly LimitsOptions _limits = limits.Value;

    public Task<Result<SubmittedDocument>> SubmitTextAsync(SubmitTextCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return StoreAsync(command.Text, DocumentTitle.Create(command.Title), pageCount: null, cancellationToken);
    }

    public async Task<Result<SubmittedDocument>> SubmitPdfAsync(SubmitPdfCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var content = await ReadWithinLimitAsync(command.Content, cancellationToken);
        if (content is null)
        {
            return Result<SubmittedDocument>.Failure(DocumentErrors.FileTooLarge(_limits.MaxFileSizeBytes));
        }

        if (!PdfSignature.Matches(content))
        {
            return Result<SubmittedDocument>.Failure(DocumentErrors.InvalidFileType);
        }

        var extracted = await textExtractor.ExtractTextAsync(content, cancellationToken);
        if (extracted.IsFailure)
        {
            return Result<SubmittedDocument>.Failure(extracted.Error!);
        }

        // Un escaneado puede traer algo de texto (número de página, pie): si no llega al mínimo, se trata como escaneado.
        if (OriginalText.CheckLength(extracted.Value.Text, _limits.TextLengthLimits) == TextLengthCheck.TooShort)
        {
            return Result<SubmittedDocument>.Failure(DocumentErrors.ScannedPdf);
        }

        var title = DocumentTitle.FromFileName(command.FileName);
        return await StoreAsync(extracted.Value.Text, title, extracted.Value.PageCount, cancellationToken);
    }

    private async Task<Result<SubmittedDocument>> StoreAsync(
        string text,
        DocumentTitle title,
        int? pageCount,
        CancellationToken cancellationToken)
    {
        var limits = _limits.TextLengthLimits;
        switch (OriginalText.CheckLength(text, limits))
        {
            case TextLengthCheck.TooShort:
                return Result<SubmittedDocument>.Failure(DocumentErrors.TextTooShort(limits.Minimum));
            case TextLengthCheck.TooLong:
                return Result<SubmittedDocument>.Failure(DocumentErrors.TextTooLong(limits.Maximum));
            case TextLengthCheck.Valid:
            default:
                break;
        }

        var originalText = OriginalText.Create(text, limits);
        var document = Document.Submit(currentUser.OwnerId, title, originalText, timeProvider.GetUtcNow());
        // TODO(US-07): las pautas que agotan su tiempo (TimedOutGuidelines) solo quedan en el log; guardarlas para que
        // "sin hallazgos" no se lea como "cumple" cuando una pauta no se ha podido comprobar.
        // TODO(US-07): indicar también cuándo se han descartado hallazgos por superar MaxFindingsPerDocument.
        var findings = guidelineChecker.Check(originalText.Value).Findings.Take(_limits.MaxFindingsPerDocument);
        document.RecordOriginalFindings(findings);
        await repository.AddAsync(document, cancellationToken);

        return Result<SubmittedDocument>.Success(
            new SubmittedDocument(document.Id, document.Status, originalText.Value.Length, pageCount));
    }

    /// <summary>Lee el fichero en memoria sin pasar del límite, aunque el tamaño declarado mienta. <c>null</c> si lo supera.</summary>
    private async Task<byte[]?> ReadWithinLimitAsync(Stream content, CancellationToken cancellationToken)
    {
        var maxBytes = _limits.MaxFileSizeBytes;
        if (content.CanSeek && content.Length > maxBytes)
        {
            return null;
        }

        using var buffer = content.CanSeek ? new MemoryStream((int)content.Length) : new MemoryStream();
        var chunk = new byte[ReadBufferSize];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        // Si la capacidad coincide con lo leído, se usa el mismo array y no se hace otra copia.
        return buffer.Capacity == buffer.Length ? buffer.GetBuffer() : buffer.ToArray();
    }
}
