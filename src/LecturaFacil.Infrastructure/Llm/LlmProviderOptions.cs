// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;
using LecturaFacil.Application.Common;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>Un proveedor de LLM con sus modelos (o despliegues) para los roles adaptador y verificador.</summary>
public class LlmProviderOptions
{
    public const int MaxDisplayNameLength = 60;

    public LlmProviderType? Type { get; set; }

    /// <summary>Nombre visible en la interfaz, por ejemplo "Servidor propio".</summary>
    public string DisplayName { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    [Secret]
    public string ApiKey { get; set; } = string.Empty;

    public string AdapterModel { get; set; } = string.Empty;

    public string VerifierModel { get; set; } = string.Empty;

    /// <summary>Valida el proveedor. Los mensajes nombran la opción, nunca su valor.</summary>
    internal IEnumerable<ValidationResult> Validate(string path)
    {
        if (Type is null)
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
