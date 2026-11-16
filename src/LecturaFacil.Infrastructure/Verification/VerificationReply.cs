// SPDX-License-Identifier: Apache-2.0
using System.Text.Json.Serialization;

namespace LecturaFacil.Infrastructure.Verification;

/// <summary>
/// Respuesta que se pide al verificador. Su esquema JSON se genera de este tipo, se envía al modelo y la respuesta se
/// valida contra él antes de usarla (OWASP LLM05). Los nombres están en español porque los lee el modelo. Los tipos,
/// estados y motivos son listas cerradas: el modelo no puede inventar motivos que se muestren al usuario.
/// Copia revisable del esquema: <c>Infrastructure/Llm/Prompts/verify.v1.schema.json</c>.
/// </summary>
public sealed record VerificationReply(
    [property: JsonPropertyName("datos_clave")] IReadOnlyList<VerificationReplyFact> KeyFacts);

/// <param name="Original">El dato tal como está en el original.</param>
/// <param name="Adapted">Dónde está en la adaptación, copiado tal cual; <c>null</c> si no aparece.</param>
public sealed record VerificationReplyFact(
    [property: JsonPropertyName("tipo")] VerificationReplyType Type,
    [property: JsonPropertyName("original")] string Original,
    [property: JsonPropertyName("adaptado")] string? Adapted,
    [property: JsonPropertyName("estado")] VerificationReplyStatus Status,
    [property: JsonPropertyName("motivo")] VerificationReplyReason Reason);

[JsonConverter(typeof(JsonStringEnumConverter<VerificationReplyType>))]
public enum VerificationReplyType
{
    [JsonStringEnumMemberName("plazo")]
    Deadline,

    [JsonStringEnumMemberName("importe")]
    Amount,

    [JsonStringEnumMemberName("requisito")]
    Requirement,

    [JsonStringEnumMemberName("fecha")]
    Date,

    [JsonStringEnumMemberName("lugar")]
    Place,

    [JsonStringEnumMemberName("documento")]
    Document,

    [JsonStringEnumMemberName("referencia")]
    Reference,

    [JsonStringEnumMemberName("otro")]
    Other,
}

[JsonConverter(typeof(JsonStringEnumConverter<VerificationReplyStatus>))]
public enum VerificationReplyStatus
{
    [JsonStringEnumMemberName("conservado")]
    Conserved,

    [JsonStringEnumMemberName("revisar")]
    Review,

    [JsonStringEnumMemberName("omitido")]
    Omitted,
}

[JsonConverter(typeof(JsonStringEnumConverter<VerificationReplyReason>))]
public enum VerificationReplyReason
{
    [JsonStringEnumMemberName("ninguno")]
    None,

    [JsonStringEnumMemberName("valor_cambiado")]
    ValueChanged,

    [JsonStringEnumMemberName("conversion_calculada")]
    CalculatedConversion,

    [JsonStringEnumMemberName("no_aparece")]
    NotInAdaptation,

    [JsonStringEnumMemberName("otro")]
    Other,
}
