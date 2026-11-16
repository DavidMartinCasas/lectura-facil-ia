// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Readability;
using LecturaFacil.Domain.Readability;

namespace LecturaFacil.Infrastructure.Readability;

/// <summary>Índice de Szigriszt-Pazos con la escala INFLESZ. Las reglas viven en Domain; aquí solo se adaptan al puerto.</summary>
internal sealed class SzigrisztPazosReadabilityCalculator : IReadabilityCalculator
{
    public Result<ReadabilityScore> Calculate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var statistics = TextStatistics.From(text);
        return statistics.HasWords
            ? Result<ReadabilityScore>.Success(SzigrisztPazosCalculator.Calculate(statistics))
            : Result<ReadabilityScore>.Failure(ReadabilityErrors.NoWords);
    }
}
