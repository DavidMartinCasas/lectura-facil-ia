// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Readability;

/// <summary>Índice del original y de la adaptación, para justificar el cambio con un dato objetivo.</summary>
public sealed record ReadabilityComparison
{
    public ReadabilityComparison(ReadabilityScore original, ReadabilityScore adapted)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(adapted);
        Original = original;
        Adapted = adapted;
    }

    public ReadabilityScore Original { get; }

    public ReadabilityScore Adapted { get; }

    /// <summary>Puntos que gana la adaptación. Negativo si es más difícil que el original.</summary>
    public double Improvement => Adapted.Value - Original.Value;
}
