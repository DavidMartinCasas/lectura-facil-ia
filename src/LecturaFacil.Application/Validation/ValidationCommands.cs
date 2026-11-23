// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Validation;

/// <summary>Envía a validación la versión <paramref name="Version"/>, que debe ser la última.</summary>
public sealed record SendToValidationCommand(DocumentId DocumentId, int Version);

/// <summary>Lo que decidió el grupo que leyó la última versión, quién era y sus observaciones.</summary>
public sealed record RecordValidationCommand(
    DocumentId DocumentId,
    ValidationOutcome Outcome,
    string ValidatedBy,
    string? Notes);
