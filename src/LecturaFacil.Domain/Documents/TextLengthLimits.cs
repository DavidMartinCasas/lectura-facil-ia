// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>Longitud mínima y máxima, en caracteres, del texto original de un documento.</summary>
public readonly record struct TextLengthLimits
{
    public TextLengthLimits(int minimum, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximum, minimum);
        Minimum = minimum;
        Maximum = maximum;
    }

    public int Minimum { get; }

    public int Maximum { get; }
}
