// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Evals.Metrics;

/// <summary>Precio de un modelo por millón de tokens, en la moneda que indique la configuración (0 si es local).</summary>
public sealed record ModelPricing(decimal InputPerMillion, decimal OutputPerMillion)
{
    public static ModelPricing Free { get; } = new(0, 0);

    public bool IsFree => InputPerMillion == 0 && OutputPerMillion == 0;
}
