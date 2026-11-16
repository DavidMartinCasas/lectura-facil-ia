// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using LecturaFacil.Domain.Verification;
using LecturaFacil.Evals.Dataset;

namespace LecturaFacil.Evals.Traps;

/// <summary>
/// Genera textos trampa de forma controlada y reproducible: en la adaptación profesional, la primera cifra entera
/// escrita solo con dígitos (un plazo, un importe…) se cambia por la siguiente ("15 días" → "16 días"). Las cifras
/// con decimales, separadores de miles, ceros a la izquierda (teléfonos como 010), fechas o escritas con letras se dejan
/// como están, para que el cambio sea exactamente uno y fácil de comprobar.
/// </summary>
public static class TrapGenerator
{
    /// <summary>Cuánto se suma a la cifra elegida.</summary>
    public const int Increment = 1;

    public static IReadOnlyList<Trap> Generate(IEnumerable<EvalText> texts, int maxTraps)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentOutOfRangeException.ThrowIfNegative(maxTraps);

        return texts.Select(Create).OfType<Trap>().Take(maxTraps).ToList().AsReadOnly();
    }

    /// <returns>La trampa, o <c>null</c> si la adaptación no tiene ninguna cifra entera escrita con dígitos.</returns>
    public static Trap? Create(EvalText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var adaptation = text.ProfessionalAdaptation;
        var figure = NumberNormalizer.Find(adaptation).FirstOrDefault(IsPlainWholeNumber);
        if (figure?.Value.Number is not { } original)
        {
            return null;
        }

        var altered = original + Increment;
        var alteredText = string.Concat(
            adaptation.AsSpan(0, figure.Start),
            altered.ToString(CultureInfo.InvariantCulture),
            adaptation.AsSpan(figure.Start + figure.Length));
        return new Trap(text, alteredText, original, altered);
    }

    private static bool IsPlainWholeNumber(NumberMention mention) =>
        mention.Value.Number is { } number
        && number == decimal.Truncate(number)
        && mention.Text.All(char.IsAsciiDigit)
        && (mention.Text.Length == 1 || mention.Text[0] != '0');
}
