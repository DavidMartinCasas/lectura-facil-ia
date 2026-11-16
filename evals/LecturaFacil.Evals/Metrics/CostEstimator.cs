// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Common;

namespace LecturaFacil.Evals.Metrics;

/// <summary>
/// Cota superior del coste de evaluar un perfil, para pedir confirmación antes de usar un modelo de pago. Sigue el peor
/// caso de <see cref="AdaptDocument"/>: el texto se parte en trozos de
/// <see cref="LimitsOptions.MaxCharactersPerAdaptationChunk"/>, cada trozo gasta sus
/// <see cref="AdaptDocument.MaxAttemptsPerChunk"/> intentos en cada pasada (la primera y cada reintento por pautas), y
/// cada respuesta llega a <see cref="LimitsOptions.MaxOutputTokens"/>. Cuenta 4 caracteres por token (a la baja en
/// castellano, que suele tener más caracteres por token). Cada trampa son dos verificaciones (con y sin la alteración).
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
        var passes = 1 + limits.MaxAdaptationRetries;
        var maxChunk = Math.Min(limits.MaxCharactersPerAdaptationChunk, limits.MaxCharacters);
        foreach (var length in originalLengths)
        {
            var chunks = Math.Min((length + maxChunk - 1) / maxChunk, limits.MaxAdaptationChunksPerDocument);
            var adapterCalls = (long)passes * chunks * AdaptDocument.MaxAttemptsPerChunk;
            // Cada llamada lleva el prompt y, como mucho, un trozo entero.
            input += adapterCalls * Tokens(adapterPromptLength + Math.Min(length, maxChunk));
            // El verificador recibe el original y una adaptación de tamaño parecido.
            input += Tokens(verifierPromptLength + (2 * length));
            output += (adapterCalls + 1) * limits.MaxOutputTokens;
        }

        foreach (var length in trapLengths)
        {
            input += 2 * Tokens(verifierPromptLength + length);
            output += 2L * limits.MaxOutputTokens;
        }

        return EvalMetrics.Cost(input, output, pricing);
    }

    private static long Tokens(int characters) => (characters + CharactersPerToken - 1) / CharactersPerToken;
}
