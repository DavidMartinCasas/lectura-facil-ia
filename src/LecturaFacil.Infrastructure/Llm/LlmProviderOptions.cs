// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;
using LecturaFacil.Application.Common;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>Un proveedor de LLM con sus modelos (o despliegues) para los roles adaptador y verificador.</summary>
public class LlmProviderOptions
{
    public const int MaxDisplayNameLength = 60;
    public const double MaxTemperature = 2;

    public LlmProviderType? Type { get; set; }

    /// <summary>Nombre visible en la interfaz, por ejemplo "Servidor propio".</summary>
    public string DisplayName { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    [Secret]
    public string ApiKey { get; set; } = string.Empty;

    public string AdapterModel { get; set; } = string.Empty;

    public string VerifierModel { get; set; } = string.Empty;

    /// <summary>Temperatura de las respuestas. 0 por defecto: adaptar y verificar piden respuestas estables.</summary>
    public double Temperature { get; set; }

    /// <summary>
    /// Pide al servidor que no razone antes de responder (<c>chat_template_kwargs.enable_thinking = false</c>). Lo
    /// entienden llama.cpp, llama-swap y vLLM con modelos híbridos como Qwen3 o Gemma 4: sin él, el razonamiento consume
    /// los tokens de salida y la respuesta llega cortada. Solo para <see cref="LlmProviderType.OpenAICompatible"/>; falso por defecto, porque otros servidores pueden rechazar el campo.
    /// </summary>
    public bool DisableThinking { get; set; }

    /// <summary>Valida el proveedor. Los mensajes nombran la opción, nunca su valor.</summary>
    internal IEnumerable<ValidationResult> Validate(string path)
    {
        if (Type is not { } type || !Enum.IsDefined(type))
        {
            yield return Error(path, nameof(Type), "es obligatorio (OpenAICompatible o AzureFoundry).");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            yield return Error(path, nameof(DisplayName), "es obligatorio.");
        }
        else if (DisplayName.Length > MaxDisplayNameLength)
        {
            yield return Error(path, nameof(DisplayName), $"no puede superar {MaxDisplayNameLength} caracteres.");
        }

        if (!ProviderEndpoint.IsAllowed(Endpoint))
        {
            yield return Error(
                path,
                nameof(Endpoint),
                "debe ser una URL absoluta https, sin credenciales (http solo hacia localhost o una red privada).");
        }

        if (DisableThinking && Type == LlmProviderType.AzureFoundry)
        {
            yield return Error(path, nameof(DisableThinking), "solo se admite con OpenAICompatible.");
        }

        if (Temperature is < 0 or > MaxTemperature || double.IsNaN(Temperature))
        {
            yield return Error(path, nameof(Temperature), $"debe estar entre 0 y {MaxTemperature}.");
        }

        foreach (var (name, value) in RequiredValues())
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield return Error(path, name, "es obligatorio.");
            }
        }
    }

    private (string Name, string Value)[] RequiredValues() =>
    [
        (nameof(ApiKey), ApiKey),
        (nameof(AdapterModel), AdapterModel),
        (nameof(VerifierModel), VerifierModel),
    ];

    private static ValidationResult Error(string path, string member, string problem) =>
        new($"{path}:{member} {problem}", [member]);
}
