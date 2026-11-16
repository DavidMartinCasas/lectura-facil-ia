// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>Estados del ciclo de vida de un documento (sección 13 del diseño).</summary>
public enum DocumentStatus
{
    Received,
    Processing,
    Error,
    Draft,
    PendingValidation,
    Validated,
    Published,
}
