// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>
/// Frases que unen varias ideas. Aproximación sin análisis sintáctico: cuenta los conectores que suelen abrir otra
/// oración (subordinadas, relativos con "cuyo", adversativas…). "y", "o" y "si" no cuentan porque casi siempre unen palabras.
/// </summary>
public sealed class OneIdeaPerSentenceRule : IGuidelineRule
{
    private static readonly TermList Connectors = new(new[]
    {
        "siempre y cuando", "a fin de que", "a menos que", "en caso de que", "de modo que", "de manera que",
        "por lo que", "por lo tanto", "por tanto", "por consiguiente", "sin embargo", "no obstante", "así como",
        "ya que", "puesto que", "dado que", "siempre que", "mientras que", "salvo que", "para que", "el cual",
        "la cual", "los cuales", "las cuales", "lo cual", "aunque", "porque", "mientras", "cuyo", "cuya", "cuyos",
        "cuyas", "pero", "sino", "además", "asimismo", "cuando", "donde",
    }.Select(connector => new TermSuggestion(connector, string.Empty)));

    private readonly int _maxConnectors;

    public OneIdeaPerSentenceRule(int maxConnectors)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConnectors);
        _maxConnectors = maxConnectors;
    }

    public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.OneIdeaPerSentence);

    public IEnumerable<Finding> Check(GuidelineText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var sentence in text.Sentences)
        {
            var connectors = Connectors.Find(sentence.Text).Select(found => found.Match.Value).ToList();
            if (connectors.Count > _maxConnectors)
            {
                var list = string.Join(", ", connectors.Distinct(StringComparer.OrdinalIgnoreCase).Select(connector => $"«{connector}»"));
                yield return Finding.At(Guideline, text.Value, sentence.Start, sentence.End,
                    $"Esta frase une varias ideas ({list}). Escribe una idea en cada frase.");
            }
        }
    }
}
