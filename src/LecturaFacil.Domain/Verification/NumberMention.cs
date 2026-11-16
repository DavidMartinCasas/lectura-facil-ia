// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Verification;

/// <summary>Una cifra en un texto: su valor normalizado, cómo aparece (con su unidad, si la tiene) y dónde.</summary>
/// <param name="Text">El fragmento tal cual, por ejemplo "250 €" o "quince".</param>
/// <param name="Start">Índice del primer carácter en el texto.</param>
public sealed record NumberMention(NumericValue Value, string Text, int Start)
{
    public int Length => Text.Length;
}
