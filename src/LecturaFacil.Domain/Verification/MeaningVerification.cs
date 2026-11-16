// SPDX-License-Identifier: Apache-2.0
using System.Globalization;

namespace LecturaFacil.Domain.Verification;

/// <summary>
/// Resultado de verificar el significado de una adaptación: la lista de datos clave con su estado. Combina lo que dice
/// el verificador con la comprobación de cifras por código, y el código siempre manda: ningún dato con una cifra
/// cambiada o perdida queda sin aviso, diga lo que diga el modelo.
/// </summary>
public sealed class MeaningVerification
{
    private MeaningVerification(VerificationStatus status, IReadOnlyList<KeyFact> keyFacts, VerificationMetadata? metadata)
    {
        Status = status;
        KeyFacts = keyFacts;
        Metadata = metadata;
    }

    public VerificationStatus Status { get; }

    /// <summary>Datos clave, de los avisos más graves a los conservados.</summary>
    public IReadOnlyList<KeyFact> KeyFacts { get; }

    /// <summary>Quién verificó; <c>null</c> si no se pudo verificar.</summary>
    public VerificationMetadata? Metadata { get; }

    public int ConservedCount => Count(KeyFactStatus.Conserved);

    public int ReviewCount => Count(KeyFactStatus.Review);

    public int OmittedCount => Count(KeyFactStatus.Omitted);

    /// <summary>Por ejemplo, "5 conservados, 1 a revisar, 1 omitido".</summary>
    public string Summary => string.Create(
        CultureInfo.InvariantCulture,
        $"{ConservedCount} {Plural(ConservedCount, "conservado")}, {ReviewCount} a revisar, {OmittedCount} {Plural(OmittedCount, "omitido")}");

    /// <summary>
    /// Combina los datos clave del verificador con la comprobación por código. Para cada dato, el estado final es el más
    /// grave (<see cref="KeyFactVerdict.MostSevere"/>). Además, cada cifra del original que el verificador no ha listado
    /// y que no está en la adaptación se añade como aviso.
    /// </summary>
    /// <param name="maxKeyFacts">Datos que se guardan como mucho; los avisos van primero para no perder ninguno.</param>
    public static MeaningVerification Combine(
        string originalText,
        string adaptedText,
        IEnumerable<KeyFact> verifierFacts,
        VerificationMetadata metadata,
        int maxKeyFacts)
    {
        ArgumentNullException.ThrowIfNull(originalText);
        ArgumentNullException.ThrowIfNull(adaptedText);
        ArgumentNullException.ThrowIfNull(verifierFacts);
        ArgumentNullException.ThrowIfNull(metadata);

        var checkedFacts = verifierFacts
            .Select(fact => fact.WithVerdict(KeyFactVerdict.MostSevere(
                NumericConsistencyCheck.Check(fact.OriginalValue, fact.AdaptedValue, adaptedText),
                fact.Verdict)))
            .ToList();
        var listed = checkedFacts.SelectMany(fact => NumberNormalizer.Values(fact.OriginalValue)).ToHashSet();
        var inAdaptation = NumberNormalizer.Values(adaptedText);
        var missing = FiguresOf(originalText)
            .Where(mention => !listed.Contains(mention.Value) && !inAdaptation.Contains(mention.Value))
            .Select(mention => FigureFact(mention, KeyFactReason.NotInAdaptation));
        return new MeaningVerification(VerificationStatus.Verified, Bounded(missing.Concat(checkedFacts), maxKeyFacts), metadata);
    }

    /// <summary>Fallo seguro: sin respuesta válida del verificador, todas las cifras del original quedan para revisar.</summary>
    public static MeaningVerification NotVerified(string originalText, int maxKeyFacts)
    {
        ArgumentNullException.ThrowIfNull(originalText);

        var facts = FiguresOf(originalText).Select(mention => FigureFact(mention, KeyFactReason.NotVerified));
        return new MeaningVerification(VerificationStatus.NotVerified, Bounded(facts, maxKeyFacts), null);
    }

    /// <summary>Reconstruye una verificación ya guardada.</summary>
    public static MeaningVerification Restore(VerificationStatus status, IEnumerable<KeyFact> keyFacts, VerificationMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(keyFacts);
        return new MeaningVerification(status, keyFacts.ToList().AsReadOnly(), metadata);
    }

    private int Count(KeyFactStatus status) => KeyFacts.Count(fact => fact.Status == status);

    private static string Plural(int count, string word) => count == 1 ? word : $"{word}s";

    /// <summary>Una mención por valor, en el orden del texto.</summary>
    private static IEnumerable<NumberMention> FiguresOf(string text) =>
        NumberNormalizer.Find(text).DistinctBy(mention => mention.Value);

    private static KeyFact FigureFact(NumberMention mention, KeyFactReason reason) =>
        KeyFact.Create(KeyFactType.Figure, mention.Text, null, new KeyFactVerdict(KeyFactStatus.Review, reason));

    private static IReadOnlyList<KeyFact> Bounded(IEnumerable<KeyFact> facts, int maxKeyFacts)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxKeyFacts);
        return facts.OrderByDescending(fact => fact.Status).Take(maxKeyFacts).ToList().AsReadOnly();
    }
}
