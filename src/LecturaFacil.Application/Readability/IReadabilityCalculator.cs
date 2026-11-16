// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Domain.Readability;

namespace LecturaFacil.Application.Readability;

/// <summary>Mide la legibilidad de un texto. Es cálculo puro, en memoria y de coste lineal: no necesita ser asíncrono.</summary>
public interface IReadabilityCalculator
{
    /// <summary>Índice del texto, o <see cref="ReadabilityErrors.NoWords"/> si no hay ninguna palabra que medir.</summary>
    Result<ReadabilityScore> Calculate(string text);
}
