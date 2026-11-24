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
    /// <summary>Valor original del aviso de <see cref="Unreadable"/>.</summary>
    public const string WholeDocument = "Todo el documento";

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

    /// <summary>Avisos que nadie ha marcado todavía como revisados. Mientras haya alguno, no se puede exportar.</summary>
    public int PendingAcknowledgementCount => KeyFacts.Count(fact => fact.RequiresAcknowledgement);

    /// <summary>Por ejemplo, "5 conservados, 1 a revisar, 1 omitido".</summary>
    public string Summary => string.Create(
        CultureInfo.InvariantCulture,
        $"{ConservedCount} {Plural(ConservedCount, "conservado")}, {ReviewCount} a revisar, {OmittedCount} {Plural(OmittedCount, "omitido")}");

    /// <summary>
    /// Combina los datos clave del verificador con la comprobación por código. Para cada dato, el estado final es el más
    /// grave (<see cref="KeyFactVerdict.MostSevere"/>). Además, cada cifra del original que el verificador no ha listado
    /// y que no está en la adaptación se añade como aviso.
    /// </summary>
    /// <param name="maxKeyFacts">Datos conservados que se guardan como mucho. Los avisos se guardan siempre todos.</param>
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

        var inAdaptation = NumberNormalizer.Values(adaptedText);
        var checkedFacts = verifierFacts
            .Select(fact => fact.WithVerdict(KeyFactVerdict.MostSevere(
                CodeVerdict(fact, originalText, adaptedText, inAdaptation),
                fact.Verdict)))
            .ToList();
        var listed = checkedFacts
            .Where(fact => NumericConsistencyCheck.Quotes(originalText, fact.OriginalValue))
            .SelectMany(fact => NumberNormalizer.Values(fact.OriginalValue))
            .ToHashSet();
        var missing = FiguresOf(originalText)
            .Where(mention => !listed.Contains(mention.Value) && !inAdaptation.Contains(mention.Value))
            .Select(mention => FigureFact(mention, KeyFactReason.FigureNotInAdaptation));
        return new MeaningVerification(VerificationStatus.Verified, Bounded(missing.Concat(checkedFacts), maxKeyFacts), metadata);
    }

    /// <summary>
    /// Une la verificación de un documento largo hecha por partes (US-23). Cada dato del verificador se comprueba con
    /// código contra su parte del original y contra la adaptación completa, y una cifra del original solo da aviso si no
    /// está en ninguna parte adaptada. Una parte que falla no oculta las demás: queda un aviso "Parte N" y sus cifras
    /// para revisar. Un dato que aparece en varias partes se queda una vez, con el veredicto más grave.
    /// Con una sola parte es lo mismo que <see cref="Combine"/> o <see cref="NotVerified"/>.
    /// </summary>
    /// <param name="adaptedText">La adaptación completa, como la leerá la persona (con el glosario).</param>
    public static MeaningVerification CombineParts(IReadOnlyList<VerificationPart> parts, string adaptedText, int maxKeyFacts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(adaptedText);
        if (parts.Count == 0)
        {
            throw new ArgumentException("There must be at least one part to combine.", nameof(parts));
        }

        if (parts.Count == 1)
        {
            var single = parts[0];
            return single.IsVerified
                ? Combine(single.OriginalText, adaptedText, single.VerifierFacts!, single.Metadata!, maxKeyFacts)
                : NotVerified(single.OriginalText, maxKeyFacts);
        }

        var inAdaptation = NumberNormalizer.Values(adaptedText);
        var facts = new List<KeyFact>();
        var listed = new HashSet<NumericValue>();
        var unlisted = new List<NumberMention>();
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            if (!part.IsVerified)
            {
                facts.Add(KeyFact.Create(KeyFactType.Other, PartLabel(index + 1), null, NotVerifiedVerdict));
                facts.AddRange(FiguresOf(part.OriginalText).Select(mention => FigureFact(mention, KeyFactReason.NotVerified)));
                continue;
            }

            var checkedFacts = part.VerifierFacts!
                .Select(fact => fact.WithVerdict(KeyFactVerdict.MostSevere(
                    CodeVerdict(fact, part.OriginalText, adaptedText, inAdaptation),
                    fact.Verdict)))
                .ToList();
            facts.AddRange(checkedFacts);
            listed.UnionWith(checkedFacts
                .Where(fact => NumericConsistencyCheck.Quotes(part.OriginalText, fact.OriginalValue))
                .SelectMany(fact => NumberNormalizer.Values(fact.OriginalValue)));
            unlisted.AddRange(FiguresOf(part.OriginalText));
        }

        var missing = unlisted
            .Where(mention => !listed.Contains(mention.Value) && !inAdaptation.Contains(mention.Value))
            .Select(mention => FigureFact(mention, KeyFactReason.FigureNotInAdaptation));
        var verified = parts.Count(part => part.IsVerified);
        var status = verified == parts.Count ? VerificationStatus.Verified
            : verified == 0 ? VerificationStatus.NotVerified
            : VerificationStatus.PartiallyVerified;
        var metadata = parts.FirstOrDefault(part => part.IsVerified)?.Metadata;
        return new MeaningVerification(status, Bounded(Deduplicated(missing.Concat(facts)), maxKeyFacts), metadata);
    }

    /// <summary>El aviso de una parte que no se ha podido verificar, por ejemplo "Parte 2".</summary>
    public static string PartLabel(int number) => string.Create(CultureInfo.InvariantCulture, $"Parte {number}");

    /// <summary>Fallo seguro: sin respuesta válida del verificador, todas las cifras del original quedan para revisar.</summary>
    public static MeaningVerification NotVerified(string originalText, int maxKeyFacts)
    {
        ArgumentNullException.ThrowIfNull(originalText);

        var facts = FiguresOf(originalText).Select(mention => FigureFact(mention, KeyFactReason.NotVerified));
        return new MeaningVerification(VerificationStatus.NotVerified, Bounded(facts, maxKeyFacts), null);
    }

    /// <summary>
    /// Fallo seguro cuando ni siquiera se han podido leer las cifras del texto (por ejemplo, un texto anómalo que agota
    /// el tiempo de búsqueda): un único aviso para que una persona revise todo el documento.
    /// </summary>
    public static MeaningVerification Unreadable() =>
        new(
            VerificationStatus.NotVerified,
            [KeyFact.Create(KeyFactType.Other, WholeDocument, null, new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.NotVerified))],
            null);

    /// <summary>Reconstruye una verificación ya guardada.</summary>
    public static MeaningVerification Restore(VerificationStatus status, IEnumerable<KeyFact> keyFacts, VerificationMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(keyFacts);
        return new MeaningVerification(status, keyFacts.ToList().AsReadOnly(), metadata);
    }

    /// <summary>
    /// La misma verificación con el dato número <paramref name="index"/> (desde 0, en el orden de <see cref="KeyFacts"/>)
    /// marcado como revisado por una persona. No cambia ningún estado: el aviso se sigue mostrando.
    /// </summary>
    public MeaningVerification AcknowledgeKeyFact(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, KeyFacts.Count);

        var facts = KeyFacts.Select((fact, position) => position == index ? fact.AsAcknowledged() : fact).ToList();
        return new MeaningVerification(Status, facts.AsReadOnly(), Metadata);
    }

    /// <summary>
    /// La misma verificación con todos los avisos marcados como revisados por una persona. Los datos conservados no
    /// cambian y los avisos se siguen mostrando.
    /// </summary>
    public MeaningVerification AcknowledgeAllKeyFacts()
    {
        var facts = KeyFacts.Select(fact => fact.AsAcknowledged()).ToList();
        return new MeaningVerification(Status, facts.AsReadOnly(), Metadata);
    }

    /// <summary>
    /// Vuelve a comprobar con código los datos clave después de una edición manual, sin llamar al verificador (US-13).
    /// Igual que en <see cref="Combine"/>, el código solo puede añadir avisos: cada dato queda con el más grave entre su
    /// estado anterior y la comprobación de su cita en el texto editado, y cada cifra del original que ya no está en la
    /// adaptación se añade como aviso. Un aviso revisado sigue revisado solo si su veredicto y su cita no han cambiado.
    /// Un dato sin cita en la adaptación (omitido o sin verificar) no se puede volver a comprobar y se queda como estaba.
    /// </summary>
    /// <param name="adaptedText">El texto editado completo, como lo leerá la persona (con el glosario).</param>
    public MeaningVerification RecheckAfterEdit(string originalText, string adaptedText, int maxKeyFacts)
    {
        ArgumentNullException.ThrowIfNull(originalText);
        ArgumentNullException.ThrowIfNull(adaptedText);

        var inAdaptation = NumberNormalizer.Values(adaptedText);
        var rechecked = KeyFacts.Select(fact => Recheck(fact, originalText, adaptedText, inAdaptation)).ToList();
        var listed = rechecked
            .Where(fact => NumericConsistencyCheck.Quotes(originalText, fact.OriginalValue))
            .SelectMany(fact => NumberNormalizer.Values(fact.OriginalValue))
            .ToHashSet();
        var missing = FiguresOf(originalText)
            .Where(mention => !listed.Contains(mention.Value) && !inAdaptation.Contains(mention.Value))
            .Select(mention => FigureFact(mention, KeyFactReason.FigureNotInAdaptation));
        return new MeaningVerification(Status, Bounded(missing.Concat(rechecked), maxKeyFacts), Metadata);
    }

    private static KeyFact Recheck(KeyFact fact, string originalText, string adaptedText, IReadOnlySet<NumericValue> inAdaptation)
    {
        if (fact.AdaptedValue is null)
        {
            return fact;
        }

        var verdict = KeyFactVerdict.MostSevere(CodeVerdict(fact, originalText, adaptedText, inAdaptation), fact.Verdict);
        var updated = fact.WithVerdict(verdict);
        var unchanged = verdict == fact.Verdict && NumericConsistencyCheck.Quotes(adaptedText, fact.AdaptedValue);
        return fact.Acknowledged && unchanged ? updated.AsAcknowledged() : updated;
    }

    /// <summary>Un dato cuya cita no está en el original es una invención del verificador: siempre se revisa.</summary>
    private static KeyFactVerdict CodeVerdict(KeyFact fact, string originalText, string adaptedText, IReadOnlySet<NumericValue> inAdaptation) =>
        NumericConsistencyCheck.Quotes(originalText, fact.OriginalValue)
            ? NumericConsistencyCheck.Check(fact.OriginalValue, fact.AdaptedValue, adaptedText, inAdaptation)
            : KeyFactVerdict.MostSevere(
                new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.NeedsReview),
                NumericConsistencyCheck.Check(fact.OriginalValue, fact.AdaptedValue, adaptedText, inAdaptation));

    private static readonly KeyFactVerdict NotVerifiedVerdict = new(KeyFactStatus.Review, KeyFactReason.NotVerified);

    /// <summary>
    /// Un dato que han encontrado varias partes (el mismo tipo y el mismo texto del original) se queda una vez, con el
    /// veredicto más grave. Se conserva el orden de la primera vez que aparece.
    /// </summary>
    private static IEnumerable<KeyFact> Deduplicated(IEnumerable<KeyFact> facts) =>
        facts
            .GroupBy(fact => (fact.Type, Key: fact.OriginalValue.ToUpperInvariant()))
            .Select(group => group.Aggregate((kept, next) =>
                KeyFactVerdict.MostSevere(kept.Verdict, next.Verdict) == next.Verdict && next.Verdict != kept.Verdict ? next : kept));

    private int Count(KeyFactStatus status) => KeyFacts.Count(fact => fact.Status == status);

    private static string Plural(int count, string word) => count == 1 ? word : $"{word}s";

    /// <summary>Una mención por valor, en el orden del texto.</summary>
    private static IEnumerable<NumberMention> FiguresOf(string text) =>
        NumberNormalizer.Find(text).DistinctBy(mention => mention.Value);

    private static KeyFact FigureFact(NumberMention mention, KeyFactReason reason) =>
        KeyFact.Create(KeyFactType.Figure, mention.Text, null, new KeyFactVerdict(KeyFactStatus.Review, reason));

    /// <summary>
    /// Los avisos van primero y nunca se descartan (están acotados por el tamaño del texto y la respuesta del modelo);
    /// solo se limita cuántos datos conservados se guardan.
    /// </summary>
    private static IReadOnlyList<KeyFact> Bounded(IEnumerable<KeyFact> facts, int maxKeyFacts)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxKeyFacts);
        var ordered = facts.OrderByDescending(fact => fact.Status).ToList();
        var warnings = ordered.Where(fact => fact.Status != KeyFactStatus.Conserved);
        var conserved = ordered.Where(fact => fact.Status == KeyFactStatus.Conserved).Take(maxKeyFacts);
        return warnings.Concat(conserved).ToList().AsReadOnly();
    }
}
