// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Validation;

/// <summary>Retira un documento de la validación sin anotar ningún resultado: vuelve a ser un borrador editable.</summary>
public sealed class WithdrawFromValidation(ValidationStep step)
{
    public Task<Result<DocumentDetails>> ExecuteAsync(DocumentId id, CancellationToken cancellationToken) =>
        step.ExecuteAsync(
            id,
            document => document.Status == DocumentStatus.PendingValidation ? null : ValidationErrors.NotPending,
            (document, now) => document.WithdrawFromValidation(now),
            cancellationToken);
}
