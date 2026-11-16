// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Domain.Readability;

namespace LecturaFacil.Application.Readability;

/// <summary>Calcula el índice INFLESZ del original y de la adaptación para mostrarlos juntos.</summary>
// TODO(US-06): llamar a este caso de uso en el pipeline tras adaptar y guardar la comparación con el documento.
public sealed class CompareReadability(IReadabilityCalculator calculator)
{
    public Result<ReadabilityComparison> Compare(string originalText, string adaptedText)
    {
        ArgumentNullException.ThrowIfNull(originalText);
        ArgumentNullException.ThrowIfNull(adaptedText);

        var original = calculator.Calculate(originalText);
        if (original.IsFailure)
        {
            return Result<ReadabilityComparison>.Failure(original.Error!);
        }

        var adapted = calculator.Calculate(adaptedText);
        if (adapted.IsFailure)
        {
            return Result<ReadabilityComparison>.Failure(adapted.Error!);
        }

        return Result<ReadabilityComparison>.Success(new ReadabilityComparison(original.Value, adapted.Value));
    }
}
