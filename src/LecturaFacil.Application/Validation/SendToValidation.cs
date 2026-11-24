// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Review;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Validation;

/// <summary>
/// Envía a validación la última versión de un borrador, cuando una persona ya ha revisado los avisos de sus datos clave.
/// El documento deja de poder editarse: lo que lee el grupo es lo que se anota después.
/// </summary>
public sealed class SendToValidation(ValidationStep step)
{
    public Task<Result<DocumentDetails>> ExecuteAsync(SendToValidationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return step.ExecuteAsync(
            command.DocumentId,
            document => Check(document, command.Version),
            (document, now) => document.SendToValidation(now),
            cancellationToken);
    }

    private static Error? Check(Document document, int expectedVersion)
    {
        if (!document.IsEditable)
        {
            return ReviewErrors.NotEditable;
        }

        if (document.LatestAdaptedVersion!.Number != expectedVersion)
        {
            return ReviewErrors.VersionConflict;
        }

        if (document.AwaitsChanges)
        {
            return ValidationErrors.Unchanged;
        }

        return document.CanSendToValidation ? null : ValidationErrors.PendingWarnings;
    }
}
