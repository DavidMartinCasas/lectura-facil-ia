// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>Transiciones válidas entre estados de un documento (sección 13 del diseño).</summary>
public static class DocumentStatusTransitions
{
    private static readonly HashSet<(DocumentStatus From, DocumentStatus To)> Allowed =
    [
        (DocumentStatus.Received, DocumentStatus.Processing),
        (DocumentStatus.Processing, DocumentStatus.Error),
        (DocumentStatus.Error, DocumentStatus.Processing),
        (DocumentStatus.Processing, DocumentStatus.Draft),
        (DocumentStatus.Draft, DocumentStatus.Draft),
        (DocumentStatus.Draft, DocumentStatus.PendingValidation),
        (DocumentStatus.PendingValidation, DocumentStatus.Validated),
        (DocumentStatus.PendingValidation, DocumentStatus.Draft),
        (DocumentStatus.Validated, DocumentStatus.Published),
    ];

    public static bool IsAllowed(DocumentStatus from, DocumentStatus to) => Allowed.Contains((from, to));
}
