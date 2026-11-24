// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Verification;

/// <summary>
/// Lo que dijo el verificador de una parte de un documento largo (US-23): los datos clave que encontró en esa parte del
/// original o, si falló, nada. <see cref="MeaningVerification.CombineParts"/> une todas las partes.
/// </summary>
public sealed record VerificationPart
{
    private VerificationPart(string originalText, IReadOnlyList<KeyFact>? verifierFacts, VerificationMetadata? metadata)
    {
        OriginalText = originalText;
        VerifierFacts = verifierFacts;
        Metadata = metadata;
    }

    /// <summary>La parte del original que se le dio al verificador.</summary>
    public string OriginalText { get; }

    /// <summary>Los datos clave según el verificador; <c>null</c> si no respondió o su respuesta no era válida.</summary>
    public IReadOnlyList<KeyFact>? VerifierFacts { get; }

    public VerificationMetadata? Metadata { get; }

    public bool IsVerified => VerifierFacts is not null;

    public static VerificationPart Verified(string originalText, IEnumerable<KeyFact> verifierFacts, VerificationMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(originalText);
        ArgumentNullException.ThrowIfNull(verifierFacts);
        ArgumentNullException.ThrowIfNull(metadata);
        return new VerificationPart(originalText, verifierFacts.ToList().AsReadOnly(), metadata);
    }

    public static VerificationPart Failed(string originalText)
    {
        ArgumentNullException.ThrowIfNull(originalText);
        return new VerificationPart(originalText, null, null);
    }
}
