// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Comprueba un documento JSON contra los esquemas que genera <see cref="JsonSchemaValidator.SchemaFor{T}"/>:
/// <c>type</c>, <c>properties</c>, <c>required</c>, <c>additionalProperties: false</c>, <c>items</c>, <c>enum</c> y
/// <c>$ref</c> locales. Cubre lo que la deserialización no comprueba (nombres exactos, nulos dentro de colecciones).
/// </summary>
internal static class JsonSchemaConformance
{
    private const int MaxDepth = 64;

    public static bool Conforms(JsonElement instance, JsonElement schema) => Conforms(instance, schema, schema, 0);

    private static bool Conforms(JsonElement instance, JsonElement schema, JsonElement root, int depth)
    {
        if (schema.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (depth > MaxDepth || schema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (schema.TryGetProperty("$ref", out var reference))
        {
            return TryResolve(root, reference.GetString(), out var target) && Conforms(instance, target, root, depth + 1);
        }

        return HasAllowedType(instance, schema)
            && IsAllowedValue(instance, schema)
            && ObjectConforms(instance, schema, root, depth)
            && ArrayConforms(instance, schema, root, depth);
    }

    private static bool HasAllowedType(JsonElement instance, JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return true;
        }

        return type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Any(allowed => IsOfType(instance, allowed.GetString()))
            : IsOfType(instance, type.GetString());
    }

    private static bool IsOfType(JsonElement instance, string? type) => type switch
    {
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => instance.ValueKind == JsonValueKind.Null,
        "number" => instance.ValueKind == JsonValueKind.Number,
        "integer" => instance.ValueKind == JsonValueKind.Number && instance.TryGetInt64(out _),
        _ => false,
    };

    private static bool IsAllowedValue(JsonElement instance, JsonElement schema) =>
        !schema.TryGetProperty("enum", out var values)
        || values.EnumerateArray().Any(value => JsonElement.DeepEquals(value, instance));

    private static bool ObjectConforms(JsonElement instance, JsonElement schema, JsonElement root, int depth)
    {
        if (instance.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        var hasProperties = schema.TryGetProperty("properties", out var properties);
        if (schema.TryGetProperty("required", out var required)
            && required.EnumerateArray().Any(name => !instance.TryGetProperty(name.GetString()!, out _)))
        {
            return false;
        }

        var closed = schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.False;
        foreach (var property in instance.EnumerateObject())
        {
            if (hasProperties && properties.TryGetProperty(property.Name, out var propertySchema))
            {
                if (!Conforms(property.Value, propertySchema, root, depth + 1))
                {
                    return false;
                }
            }
            else if (closed)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ArrayConforms(JsonElement instance, JsonElement schema, JsonElement root, int depth) =>
        instance.ValueKind != JsonValueKind.Array
        || !schema.TryGetProperty("items", out var items)
        || instance.EnumerateArray().All(item => Conforms(item, items, root, depth + 1));

    private static bool TryResolve(JsonElement root, string? reference, out JsonElement target)
    {
        target = root;
        if (reference is null || !reference.StartsWith('#'))
        {
            return false;
        }

        foreach (var segment in reference[1..].Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (target.ValueKind != JsonValueKind.Object || !target.TryGetProperty(name, out target))
            {
                return false;
            }
        }

        return true;
    }
}
