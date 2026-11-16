// SPDX-License-Identifier: Apache-2.0
using System.Text.Json.Serialization;

namespace LecturaFacil.Application.Adaptation;

/// <summary>
/// Respuesta que se pide al modelo adaptador. Su esquema JSON se genera de este tipo, se envía al modelo y la respuesta
/// se valida contra él antes de usarla (OWASP LLM05). Los nombres de los campos están en español porque los lee el modelo.
/// Copia revisable del esquema: <c>Infrastructure/Llm/Prompts/adapter.v1.schema.json</c>.
/// </summary>
public sealed record AdaptationReply(
    [property: JsonPropertyName("titulo")] string Title,
    [property: JsonPropertyName("secciones")] IReadOnlyList<AdaptationReplySection> Sections,
    [property: JsonPropertyName("glosario")] IReadOnlyList<AdaptationReplyTerm> Glossary);

public sealed record AdaptationReplySection(
    [property: JsonPropertyName("pregunta")] string Question,
    [property: JsonPropertyName("frases")] IReadOnlyList<string> Sentences);

public sealed record AdaptationReplyTerm(
    [property: JsonPropertyName("termino")] string Term,
    [property: JsonPropertyName("definicion")] string Definition);
