// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Evals.Metrics;

/// <summary>
/// Cota superior del coste de evaluar un perfil, para pedir confirmación antes de usar un modelo de pago. Supone que
/// cada texto agota sus reintentos, que cada respuesta llega a <see cref="LimitsOptions.MaxOutputTokens"/> y 4
/// caracteres por token.
/// </summary>
public static class CostEstimator
{
    public const int CharactersPerToken = 4;

    /// <param name="originalLengths">Caracteres de cada texto original.</param>
    /// <param name="trapLengths">Caracteres del original más la adaptación de cada texto trampa.</param>
    /// <param name="adapterPromptLength">Caracteres del prompt del adaptador con el de reintento.</param>
    /// <param name="verifierPromptLength">Caracteres del prompt del verificador.</param>
    public static decimal Estimate(
        IEnumerable<int> originalLengths,
        IEnumerable<int> trapLengths,
        int adapterPromptLength,
        int verifierPromptLength,
        LimitsOptions limits,
        ModelPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(originalLengths);
        ArgumentNullException.ThrowIfNull(trapLengths);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(pricing);

        long input = 0, output = 0;
        var adapterCalls = 1 + limits.MaxAdaptationRetries;
        foreach (var length in originalLengths)
        {
            input += adapterCalls * Tokens(adapterPromptLength + length);
            // El verificador recibe el original y una adaptación de tamaño parecido.
            input += Tokens(verifierPromptLength + (2 * length));
            output += (adapterCalls + 1L) * limits.MaxOutputTokens;
        }

        foreach (var length in trapLengths)
        {
            input += Tokens(verifierPromptLength + length);
            output += limits.MaxOutputTokens;
        }

        return EvalMetrics.Cost(input, output, pricing);
    }

    private static long Tokens(int characters) => (characters + CharactersPerToken - 1) / CharactersPerToken;
}
