// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Evals.Metrics;

/// <summary>
/// Métricas de la evaluación (sección 18 del documento de diseño). Son funciones puras: lo que mide cada una depende
/// solo de sus argumentos. <c>null</c> significa "no se puede medir" (por ejemplo, un texto sin datos clave) y no cuenta
/// en las medias.
/// </summary>
public static class EvalMetrics
{
    private const decimal TokensPerMillion = 1_000_000m;

    /// <summary>Pautas cumplidas entre las revisadas por el revisor determinista (0 a 1).</summary>
    public static double? GuidelineCompliance(GuidelineReport? report) =>
        report is null || report.TotalCount == 0 ? null : (double)report.CompliantCount / report.TotalCount;

    /// <summary>Datos clave conservados entre todos los que ha listado la verificación (0 a 1).</summary>
    public static double? KeyFactRetention(MeaningVerification? verification) =>
        verification is null || verification.KeyFacts.Count == 0
            ? null
            : (double)verification.ConservedCount / verification.KeyFacts.Count;

    /// <summary>Datos clave cambiados, a revisar u omitidos: los avisos que la persona tendría que revisar.</summary>
    public static int KeyFactWarnings(MeaningVerification? verification) =>
        verification is null ? 0 : verification.ReviewCount + verification.OmittedCount;

    /// <summary>Similitud coseno entre dos vectores (−1 a 1). Si alguno es nulo, 0.</summary>
    public static double CosineSimilarity(IReadOnlyList<float> first, IReadOnlyList<float> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.Count != second.Count)
        {
            throw new ArgumentException("Both vectors must have the same dimension.", nameof(second));
        }

        double dot = 0, firstNorm = 0, secondNorm = 0;
        for (var i = 0; i < first.Count; i++)
        {
            dot += (double)first[i] * second[i];
            firstNorm += (double)first[i] * first[i];
            secondNorm += (double)second[i] * second[i];
        }

        return firstNorm == 0 || secondNorm == 0 ? 0 : dot / (Math.Sqrt(firstNorm) * Math.Sqrt(secondNorm));
    }

    /// <summary>Recall del verificador en los textos trampa: avisos detectados / cambios introducidos.</summary>
    public static double? Recall(int detected, int introduced)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(detected);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(detected, introduced);
        return introduced == 0 ? null : (double)detected / introduced;
    }

    /// <summary>Media de los valores que se han podido medir.</summary>
    public static double? Mean(IEnumerable<double?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var measured = values.OfType<double>().ToList();
        return measured.Count == 0 ? null : measured.Average();
    }

    /// <summary>Coste estimado de unos tokens con el precio por millón del modelo.</summary>
    public static decimal Cost(long inputTokens, long outputTokens, ModelPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokens);
        return ((inputTokens * pricing.InputPerMillion) + (outputTokens * pricing.OutputPerMillion)) / TokensPerMillion;
    }

    /// <summary>Tokens generados por segundo de respuesta del modelo.</summary>
    public static double? TokensPerSecond(long outputTokens, TimeSpan duration) =>
        duration <= TimeSpan.Zero ? null : outputTokens / duration.TotalSeconds;
}
