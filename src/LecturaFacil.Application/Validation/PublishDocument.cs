// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Validation;

/// <summary>Publica un documento validado: su última versión es la final y ya no cambia.</summary>
public sealed class PublishDocument(ValidationStep step)
{
    public Task<Result<DocumentDetails>> ExecuteAsync(DocumentId id, CancellationToken cancellationToken) =>
        step.ExecuteAsync(
            id,
            document => document.Status == DocumentStatus.Validated ? null : ValidationErrors.NotValidated,
            (document, now) => document.Publish(now),
            cancellationToken);
}
