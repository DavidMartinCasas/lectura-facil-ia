// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Evals.Metrics;

namespace LecturaFacil.Evals.Tests.Metrics;

public sealed class CostEstimatorTests
{
    private static readonly LimitsOptions Limits = new() { MaxAdaptationRetries = 1, MaxOutputTokens = 1_000 };

    [Fact]
    public void Estimate_WhenModelIsFree_ReturnsZero()
    {
        CostEstimator.Estimate([4_000], [8_000], 400, 400, Limits, ModelPricing.Free).ShouldBe(0m);
    }

    [Fact]
    public void Estimate_WhenModelIsPaid_CountsEveryCallAtItsMaximumOutput()
    {
        // Texto de un trozo: 2 pasadas × 2 intentos = 4 llamadas de (400 + 4000) / 4 = 1100 tokens y una verificación de
        // (400 + 8000) / 4 = 2100 tokens, con 5 respuestas de 1000. Trampa: 2 verificaciones de 2100 y 2 respuestas.
        var pricing = new ModelPricing(InputPerMillion: 1_000_000m, OutputPerMillion: 0m);
        var input = CostEstimator.Estimate([4_000], [8_000], 400, 400, Limits, pricing);
        var output = CostEstimator.Estimate([4_000], [8_000], 400, 400, Limits, new ModelPricing(0m, 1_000_000m));

        input.ShouldBe((4 * 1_100) + 2_100 + (2 * 2_100));
        output.ShouldBe(7 * 1_000);
    }

    [Fact]
    public void Estimate_WhenTextNeedsSeveralChunks_CountsEveryAttemptOfEveryChunk()
    {
        var limits = new LimitsOptions { MaxAdaptationRetries = 1, MaxOutputTokens = 1_000, MaxCharactersPerAdaptationChunk = 6_000, MaxCharacters = 20_000 };
        var pricing = new ModelPricing(0m, 1_000_000m);

        // 20 000 caracteres = 4 trozos × 2 intentos × 2 pasadas = 16 llamadas al adaptador, más la verificación.
        CostEstimator.Estimate([20_000], [], 400, 400, limits, pricing).ShouldBe(17 * 1_000);
    }

    [Fact]
    public void Estimate_WhenThereAreMoreTexts_CostsMore()
    {
        var pricing = new ModelPricing(0.44m, 1.76m);

        CostEstimator.Estimate([1_000, 1_000], [], 400, 400, Limits, pricing)
            .ShouldBeGreaterThan(CostEstimator.Estimate([1_000], [], 400, 400, Limits, pricing));
    }
}
