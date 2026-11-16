// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Verification;

/// <summary>
/// Comprueba con código las cifras de un dato clave, sin fiarse del verificador (OWASP LLM09): cada cifra del fragmento
/// original debe estar en el fragmento adaptado, y ese fragmento debe estar de verdad en la adaptación, tal cual. Así el
/// verificador no puede tapar un cambio citando un texto que no existe.
/// Compara valores normalizados (<see cref="NumberNormalizer"/>): "quince días" y "15 días" son el mismo valor.
/// </summary>
public static class NumericConsistencyCheck
{
    /// <param name="originalFragment">El dato tal como está en el original.</param>
    /// <param name="adaptedFragment">Dónde dice el verificador que está en la adaptación; vacío si no está.</param>
    /// <param name="adaptedText">La adaptación completa.</param>
    public static KeyFactVerdict Check(string originalFragment, string? adaptedFragment, string adaptedText)
    {
        ArgumentNullException.ThrowIfNull(adaptedText);
        return Check(originalFragment, adaptedFragment, adaptedText, NumberNormalizer.Values(adaptedText));
    }

    /// <param name="adaptedTextValues">Las cifras de <paramref name="adaptedText"/>, calculadas una sola vez para todos los datos.</param>
    internal static KeyFactVerdict Check(
        string originalFragment, string? adaptedFragment, string adaptedText, IReadOnlySet<NumericValue> adaptedTextValues)
    {
        ArgumentNullException.ThrowIfNull(originalFragment);
        ArgumentNullException.ThrowIfNull(adaptedText);

        if (string.IsNullOrWhiteSpace(adaptedFragment))
        {
            return new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation);
        }

        var originalValues = NumberNormalizer.Values(originalFragment);
        var adaptedValues = NumberNormalizer.Values(adaptedFragment);
        if (!Quotes(adaptedText, adaptedFragment) || !adaptedValues.IsSubsetOf(adaptedTextValues))
        {
            return new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.QuoteNotFound);
        }

        if (!originalValues.IsSubsetOf(adaptedValues))
        {
            return new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.ValueChanged);
        }

        return originalValues.Count == 0 || QualifierCheck.Keeps(originalFragment, adaptedFragment)
            ? KeyFactVerdict.Conserved
            : new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.QualifierChanged);
    }

    /// <summary>Si <paramref name="fragment"/> está en <paramref name="text"/>, sin distinguir mayúsculas, saltos de línea ni espacios repetidos.</summary>
    public static bool Quotes(string text, string fragment)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fragment);
        return Normalize(text).Contains(Normalize(fragment), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Compara sin tener en cuenta los saltos de línea ni los espacios repetidos.</summary>
    private static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
