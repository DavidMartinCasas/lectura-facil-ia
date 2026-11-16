// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Verification;

/// <summary>Quién verificó la adaptación (<c>modelo_verificador</c>) y con qué prompt.</summary>
/// <param name="Provider">Nombre visible del proveedor que respondió.</param>
/// <param name="Model">Modelo (o despliegue) verificador.</param>
/// <param name="PromptVersion">Prompt usado, por ejemplo <c>verify.v1</c>.</param>
public sealed record VerificationMetadata(string Provider, string Model, string PromptVersion);
