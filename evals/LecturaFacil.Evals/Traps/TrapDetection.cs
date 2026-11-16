// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Evals.Traps;

/// <summary>Qué ha pasado con un texto trampa.</summary>
public enum TrapOutcome
{
    /// <summary>Nadie ha avisado del cambio.</summary>
    Missed,

    /// <summary>Solo lo ha avisado la comprobación de cifras por código (la cifra original ya no está en la adaptación).</summary>
    DetectedByCode,

    /// <summary>El verificador ha listado el dato cambiado (el estado final puede venir también del código).</summary>
    DetectedByVerifier,

    /// <summary>
    /// El verificador no respondió o su respuesta no era válida: todas las cifras quedan "a revisar" por fallo seguro, y
    /// eso no cuenta como detección. No entra en el recall.
    /// </summary>
    NotVerified,
}

/// <summary>Decide si la verificación de un texto trampa ha avisado del cambio introducido.</summary>
public static class TrapDetection
{
    /// <summary>
    /// Un aviso cuenta si cita la cifra cambiada en la adaptación, o si cita la cifra original y la verificación de la
    /// adaptación profesional sin alterar (<paramref name="baseline"/>, el grupo de control) no tenía ya un aviso con
    /// esa cifra. Así un aviso que no tiene nada que ver con la trampa no se cuenta como detección.
    /// </summary>
    public static TrapOutcome Classify(MeaningVerification verification, MeaningVerification baseline, Trap trap)
    {
        ArgumentNullException.ThrowIfNull(verification);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(trap);

        if (verification.Status != VerificationStatus.Verified)
        {
            return TrapOutcome.NotVerified;
        }

        var original = NumericValue.Of(trap.OriginalFigure);
        var altered = NumericValue.Of(trap.AlteredFigure);
        var originalWarnedBefore = Warnings(baseline).Any(fact => Cites(fact, original));
        var detecting = Warnings(verification)
            .Where(fact => Quotes(fact.AdaptedValue, altered) || (!originalWarnedBefore && Cites(fact, original)))
            .ToList();
        if (detecting.Count == 0)
        {
            return TrapOutcome.Missed;
        }

        // Los avisos con FigureNotInAdaptation los añade solo el código; cualquier otro es un dato que listó el verificador.
        return detecting.Exists(fact => fact.Reason != KeyFactReason.FigureNotInAdaptation)
            ? TrapOutcome.DetectedByVerifier
            : TrapOutcome.DetectedByCode;
    }

    private static IEnumerable<KeyFact> Warnings(MeaningVerification verification) =>
        verification.KeyFacts.Where(fact => fact.Status != KeyFactStatus.Conserved);

    private static bool Cites(KeyFact fact, NumericValue figure) =>
        Quotes(fact.OriginalValue, figure) || Quotes(fact.AdaptedValue, figure);

    private static bool Quotes(string? value, NumericValue figure) => NumberNormalizer.Values(value).Contains(figure);
}
