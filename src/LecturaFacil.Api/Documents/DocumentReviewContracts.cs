// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Api.Documents;

/// <summary>
/// La adaptación completa, editada a mano: título, secciones (pregunta y frases) y glosario. Es texto plano; el servidor
/// lo normaliza igual que una adaptación del modelo y nunca lo interpreta como HTML ni Markdown.
/// </summary>
public sealed record ManualEditRequest(
    string? Title,
    IReadOnlyList<ManualEditSectionRequest?>? Sections,
    IReadOnlyList<ManualEditGlossaryTermRequest?>? Glossary);

public sealed record ManualEditSectionRequest(string? Question, IReadOnlyList<string?>? Sentences);

public sealed record ManualEditGlossaryTermRequest(string? Term, string? Definition);

/// <summary>Versión que se está revisando; debe ser la última del documento. Sirve para marcar un aviso o todos.</summary>
public sealed record AcknowledgeKeyFactRequest(int? Version);
