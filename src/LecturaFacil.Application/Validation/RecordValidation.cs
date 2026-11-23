// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
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
            document => document.Status == DocumentStatus.PendingValidation ? null : ValidationErrors.NotPending,
            (document, now) => document.RecordValidation(command.Outcome, command.ValidatedBy, command.Notes, now),
            cancellationToken);
    }
}
