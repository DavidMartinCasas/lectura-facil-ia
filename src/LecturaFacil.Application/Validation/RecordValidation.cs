// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Review;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Validation;

/// <summary>
/// Anota lo que decidió el grupo de validación: si lo entiende, el documento queda validado; si pide cambios, vuelve a
/// borrador. La validación se hace fuera de la aplicación; la anota la misma persona que adaptó el documento.
/// </summary>
public sealed class RecordValidation(ValidationStep step)
{
    public Task<Result<DocumentDetails>> ExecuteAsync(RecordValidationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return step.ExecuteAsync(
            command.DocumentId,
            document => Check(document, command.Version),
            (document, now) => document.RecordValidation(command.Outcome, command.ValidatedBy, command.Notes, now),
            cancellationToken);
    }

    /// <summary>
    /// El resultado es de la versión que leyó el grupo. Si mientras tanto el documento se ha retirado, cambiado y vuelto
    /// a enviar desde otra pestaña, la versión pendiente es otra y no se le puede asignar ese resultado.
    /// </summary>
    private static Error? Check(Document document, int expectedVersion)
    {
        if (document.Status != DocumentStatus.PendingValidation)
        {
            return ValidationErrors.NotPending;
        }

        return document.LatestAdaptedVersion!.Number == expectedVersion ? null : ReviewErrors.VersionConflict;
    }
}
