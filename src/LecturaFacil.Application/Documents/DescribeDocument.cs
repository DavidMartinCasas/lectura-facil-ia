// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Readability;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Documents;

/// <summary>
/// Lo que se muestra de un documento: su estado, el original con sus problemas, la última versión y la legibilidad de
/// los dos textos. La legibilidad se calcula al leer (es cálculo en memoria y de coste lineal), así siempre corresponde
/// a la última versión, también después de una edición manual.
/// </summary>
public sealed class DescribeDocument(CompareReadability compareReadability)
{
    public DocumentDetails Execute(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var failure = document.Status == DocumentStatus.Error && document.FailureCode is { } code
            ? AdaptationErrors.FromFailureCode(code)
            : null;
        var latest = document.LatestAdaptedVersion;
        var readability = latest is null ? null : compareReadability.Compare(document.OriginalText.Value, latest.PlainText);
        return new DocumentDetails(
            document.Id,
            document.Title.Value,
            document.Status,
            document.CreatedAt,
            document.UpdatedAt,
            failure,
            latest,
            document.OriginalText.Value,
            document.OriginalFindings,
            readability is { IsSuccess: true } ? readability.Value : null);
    }
}
