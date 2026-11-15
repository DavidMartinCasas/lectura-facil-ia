// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Documents;

/// <summary>Extrae el texto de un PDF digital, en memoria y con límites de páginas y de tiempo.</summary>
public interface ITextExtractor
{
    /// <returns>
    /// El texto con los párrafos separados por una línea en blanco, o <see cref="DocumentErrors.ScannedPdf"/>,
    /// <see cref="DocumentErrors.UnreadablePdf"/> o <see cref="DocumentErrors.PdfTooComplex"/>.
    /// </returns>
    Task<Result<ExtractedText>> ExtractTextAsync(byte[] pdf, CancellationToken cancellationToken);
}

public sealed record ExtractedText(string Text, int PageCount);
