// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Review;

/// <summary>Adaptación editada a mano sobre la versión <paramref name="Version"/>, que debe ser la última.</summary>
public sealed record SaveManualEditCommand(
    DocumentId DocumentId,
    int Version,
    string Title,
    IReadOnlyList<AdaptedSectionDraft> Sections,
    IReadOnlyList<GlossaryTermDraft> Glossary);

/// <summary>
/// Marca como revisado el dato clave número <paramref name="KeyFactIndex"/> (desde 0) de la versión
/// <paramref name="Version"/>, que debe ser la última.
/// </summary>
public sealed record AcknowledgeKeyFactCommand(DocumentId DocumentId, int Version, int KeyFactIndex);

/// <summary>
/// Marca como revisados todos los avisos de los datos clave de la versión <paramref name="Version"/>, que debe ser la
/// última.
/// </summary>
public sealed record AcknowledgeAllKeyFactsCommand(DocumentId DocumentId, int Version);
