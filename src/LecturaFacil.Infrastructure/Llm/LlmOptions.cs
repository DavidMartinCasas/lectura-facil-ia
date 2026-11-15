// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Proveedores de LLM. El principal es obligatorio; el respaldo solo se valida (y solo se usará) si está habilitado.
/// </summary>
public sealed class LlmOptions : IValidatableObject
{
    public const string SectionName = "Llm";

    public LlmProviderOptions Primary { get; set; } = new();

    public FallbackLlmProviderOptions Fallback { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = Primary.Validate($"{SectionName}:{nameof(Primary)}");
        return Fallback.Enabled
            ? results.Concat(Fallback.Validate($"{SectionName}:{nameof(Fallback)}"))
            : results;
    }
}

/// <summary>Proveedor de respaldo. Deshabilitado por defecto: si no se habilita, ningún documento sale del principal.</summary>
public sealed class FallbackLlmProviderOptions : LlmProviderOptions
{
    public bool Enabled { get; set; }
}
