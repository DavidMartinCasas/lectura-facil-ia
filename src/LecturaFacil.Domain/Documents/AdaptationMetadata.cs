// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>Cómo se generó una adaptación: quién respondió, con qué prompt y cuánto costó.</summary>
/// <param name="Provider">Nombre visible del proveedor que respondió.</param>
/// <param name="Model">Modelo (o despliegue) que respondió.</param>
/// <param name="PromptVersion">Prompt usado, por ejemplo <c>adapter.v1</c>.</param>
public sealed record AdaptationMetadata(
    string Provider,
    string Model,
    string PromptVersion,
    long InputTokens,
    long OutputTokens,
    TimeSpan Duration)
{
    /// <summary>Longitud máxima del proveedor, del modelo y de la versión del prompt.</summary>
    public const int NameMaxLength = 200;
}
