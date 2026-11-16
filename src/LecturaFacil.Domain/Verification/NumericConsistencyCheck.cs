// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Verification;

/// <summary>
/// Comprueba con código las cifras de un dato clave, sin fiarse del verificador (OWASP LLM09): cada cifra del fragmento
/// original debe estar en el fragmento adaptado, y cada cifra del fragmento adaptado debe estar de verdad en la adaptación.
/// Compara valores normalizados (<see cref="NumberNormalizer"/>): "quince días" y "15 días" son el mismo valor.
/// </summary>
public static class NumericConsistencyCheck
{
    /// <param name="originalFragment">El dato tal como está en el original.</param>
    /// <param name="adaptedFragment">Dónde dice el verificador que está en la adaptación; vacío si no está.</param>
    /// <param name="adaptedText">La adaptación completa.</param>
    public static KeyFactVerdict Check(string originalFragment, string? adaptedFragment, string adaptedText)
    {
        ArgumentNullException.ThrowIfNull(originalFragment);
        ArgumentNullException.ThrowIfNull(adaptedText);

        if (string.IsNullOrWhiteSpace(adaptedFragment))
        {
            return new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation);
        }

        var adaptedValues = NumberNormalizer.Values(adaptedFragment);
        if (!adaptedValues.IsSubsetOf(NumberNormalizer.Values(adaptedText)))
        {
            return new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.NotInAdaptation);
        }

        return NumberNormalizer.Values(originalFragment).IsSubsetOf(adaptedValues)
            ? KeyFactVerdict.Conserved
            : new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.ValueChanged);
    }
}
