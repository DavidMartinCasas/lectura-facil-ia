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
        // Texto: 2 adaptaciones de (400 + 4000) / 4 = 1100 tokens y una verificación de (400 + 8000) / 4 = 2100 tokens,
        // con 3 respuestas de 1000 tokens. Trampa: (400 + 8000) / 4 = 2100 tokens y 1000 de respuesta.
        var pricing = new ModelPricing(InputPerMillion: 1_000_000m, OutputPerMillion: 0m);
        var input = CostEstimator.Estimate([4_000], [8_000], 400, 400, Limits, pricing);
        var output = CostEstimator.Estimate([4_000], [8_000], 400, 400, Limits, new ModelPricing(0m, 1_000_000m));

        input.ShouldBe((2 * 1_100) + 2_100 + 2_100);
        output.ShouldBe(4 * 1_000);
    }

    [Fact]
    public void Estimate_WhenThereAreMoreTexts_CostsMore()
    {
        var pricing = new ModelPricing(0.44m, 1.76m);

        CostEstimator.Estimate([1_000, 1_000], [], 400, 400, Limits, pricing)
            .ShouldBeGreaterThan(CostEstimator.Estimate([1_000], [], 400, 400, Limits, pricing));
    }
}
