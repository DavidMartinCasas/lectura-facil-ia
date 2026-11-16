// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Evals.Traps;

/// <summary>Decide si la verificación de un texto trampa ha avisado del cambio introducido.</summary>
public static class TrapDetection
{
    /// <summary>
    /// Hay aviso si algún dato clave que no está marcado como conservado cita la cifra original o la cambiada, en el
    /// original o en la adaptación.
    /// </summary>
    public static bool IsDetected(MeaningVerification verification, Trap trap)
    {
        ArgumentNullException.ThrowIfNull(verification);
        ArgumentNullException.ThrowIfNull(trap);

        var figures = new[] { NumericValue.Of(trap.OriginalFigure), NumericValue.Of(trap.AlteredFigure) };
        return verification.KeyFacts
            .Where(fact => fact.Status != KeyFactStatus.Conserved)
            .Any(fact => Quotes(fact.OriginalValue, figures) || Quotes(fact.AdaptedValue, figures));
    }

    private static bool Quotes(string? value, NumericValue[] figures)
    {
        var values = NumberNormalizer.Values(value);
        return figures.Any(values.Contains);
    }
}
