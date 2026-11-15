// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// El esquema de la respuesta es el del tipo C# que espera el caso de uso: se envía al modelo y la respuesta se
/// valida deserializando en modo estricto (campos obligatorios, tipos exactos, sin nulos inesperados ni campos
/// desconocidos). La salida del modelo no se usa hasta pasar esta validación (OWASP LLM05).
/// </summary>
public static class JsonSchemaValidator
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static readonly JsonSchemaExporterOptions ExporterOptions = new() { TreatNullObliviousAsNonNullable = true };

    public static JsonElement SchemaFor<T>() => SchemaCache<T>.Schema;

    public static Result<T> Validate<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<T>.Failure(LlmErrors.InvalidProviderOutput);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) is { } value
                ? Result<T>.Success(value)
                : Result<T>.Failure(LlmErrors.InvalidProviderOutput);
        }
        catch (JsonException)
        {
            return Result<T>.Failure(LlmErrors.InvalidProviderOutput);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.Strict,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.MakeReadOnly();
        return options;
    }

    private static class SchemaCache<T>
    {
        public static readonly JsonElement Schema =
            JsonSerializer.SerializeToElement(Options.GetJsonSchemaAsNode(typeof(T), ExporterOptions));
    }
}
