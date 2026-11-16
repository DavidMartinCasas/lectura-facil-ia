// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Review;

/// <summary>Comprobaciones comunes antes de cambiar un borrador.</summary>
internal static class ReviewGuard
{
    /// <summary>
    /// La última versión, si el documento es un borrador editable y <paramref name="expectedVersion"/> es esa versión.
    /// Si otra pestaña o persona ha guardado antes una versión nueva, se avisa en lugar de pisarla.
    /// </summary>
    public static Result<AdaptedVersion> LatestEditableVersion(Document document, int expectedVersion)
    {
        if (!document.IsEditable)
        {
            return Result<AdaptedVersion>.Failure(ReviewErrors.NotEditable);
        }

        var latest = document.LatestAdaptedVersion!;
        return latest.Number == expectedVersion
            ? Result<AdaptedVersion>.Success(latest)
            : Result<AdaptedVersion>.Failure(ReviewErrors.VersionConflict);
    }
}
