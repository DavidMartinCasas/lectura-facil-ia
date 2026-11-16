// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Application.Verification;

/// <summary>
/// Puerto hacia el verificador del significado: un modelo de otra familia que el adaptador (ADR 0002) extrae los datos
/// clave del original y dice si están en la adaptación. Su opinión no es definitiva: <see cref="VerifyMeaning"/> la
/// combina con la comprobación de cifras por código.
/// </summary>
public interface IMeaningVerifier
{
    /// <returns>
    /// Los datos clave según el verificador, o <c>LlmErrors.InvalidProviderOutput</c> si la respuesta no cumple el
    /// esquema, o <c>LlmErrors.ProviderUnavailable</c> si el proveedor no responde.
    /// </returns>
    Task<Result<VerifierAssessment>> AssessAsync(string originalText, string adaptedText, CancellationToken cancellationToken);
}

/// <summary>Los datos clave que ha encontrado el verificador y quién los ha encontrado.</summary>
public sealed record VerifierAssessment(IReadOnlyList<KeyFact> KeyFacts, VerificationMetadata Metadata);
