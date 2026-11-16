// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>
/// Títulos claros en la adaptación. No es una regla de texto (<see cref="IGuidelineRule"/>): necesita saber qué líneas
/// son títulos, así que solo se aplica a un <see cref="AdaptedContent"/>, nunca al original. Cada sección tiene siempre
/// un título (lo garantiza <see cref="AdaptedContent"/>); aquí se comprueba que sea corto: como mucho el mismo número de
/// palabras que una frase. Las posiciones de los hallazgos se refieren a <see cref="AdaptedContent.PlainText"/>.
/// </summary>
public sealed class ClearHeadingsRule
{
    private readonly int _maxWordsPerHeading;

    public ClearHeadingsRule(int maxWordsPerHeading)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWordsPerHeading);
        _maxWordsPerHeading = maxWordsPerHeading;
    }

    public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.ClearHeadings);

    public IEnumerable<Finding> Check(AdaptedContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var text = content.PlainText;
        var findings = new List<Finding>();
        var position = content.Title.Length + 1;
        foreach (var section in content.Sections)
        {
            if (Words.Count(section.Question) > _maxWordsPerHeading)
            {
                findings.Add(Finding.At(Guideline, text, position, position + section.Question.Length,
                    $"El título de la sección es largo. Usa una pregunta corta, de {_maxWordsPerHeading} palabras como mucho, por ejemplo «¿Quién puede pedir la ayuda?»."));
            }

            position += section.Question.Length + 1 + section.Sentences.Sum(sentence => sentence.Length + 1);
        }

        return findings;
    }
}
