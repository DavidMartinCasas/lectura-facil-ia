// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Validation;

/// <summary>Envía a validación la versión <paramref name="Version"/>, que debe ser la última.</summary>
public sealed record SendToValidationCommand(DocumentId DocumentId, int Version);

/// <summary>
/// Lo que decidió el grupo que leyó la versión <paramref name="Version"/>, quién era y sus observaciones. La versión debe
/// seguir siendo la última: si el documento se ha retirado, cambiado y vuelto a enviar, el resultado no es de esta.
/// </summary>
public sealed record RecordValidationCommand(
    DocumentId DocumentId,
    int Version,
    ValidationOutcome Outcome,
    string ValidatedBy,
    string? Notes);
