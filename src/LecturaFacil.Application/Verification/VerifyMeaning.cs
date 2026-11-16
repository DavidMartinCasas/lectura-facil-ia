// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;
using LecturaFacil.Application.Common;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Verification;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Verification;

/// <summary>
/// Verifica que una adaptación conserva los datos clave del original (US-11). Pide su opinión al verificador y la
/// combina con la comprobación de cifras por código (<see cref="MeaningVerification.Combine"/>): el verificador solo
/// puede añadir avisos. Nunca falla: si el verificador no responde, su respuesta no es válida o los textos son
/// demasiado largos, todas las cifras del original quedan para revisar (fallo seguro). No cambia la adaptación.
/// </summary>
public sealed class VerifyMeaning(IMeaningVerifier verifier, IOptions<LimitsOptions> limits)
{
    private readonly LimitsOptions _limits = limits.Value;

    public async Task<MeaningVerification> ExecuteAsync(string originalText, AdaptedContent adaptation, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalText);
        ArgumentNullException.ThrowIfNull(adaptation);

        var adaptedText = TextToVerify(adaptation);
        try
        {
            if (originalText.Length + adaptedText.Length > _limits.MaxCharactersPerVerification)
            {
                return MeaningVerification.NotVerified(originalText, _limits.MaxKeyFactsPerVersion);
            }

            var assessment = await verifier.AssessAsync(originalText, adaptedText, cancellationToken);
            return assessment.IsSuccess
                ? MeaningVerification.Combine(
                    originalText, adaptedText, assessment.Value.KeyFacts, assessment.Value.Metadata, _limits.MaxKeyFactsPerVersion)
                : MeaningVerification.NotVerified(originalText, _limits.MaxKeyFactsPerVersion);
        }
        catch (RegexMatchTimeoutException)
        {
            // Un texto anómalo no debe tirar el borrador: se pide revisar todo el documento.
            return MeaningVerification.Unreadable();
        }
    }

    /// <summary>Todo lo que leerá la persona: el texto adaptado y, después, el glosario.</summary>
    public static string TextToVerify(AdaptedContent adaptation)
    {
        ArgumentNullException.ThrowIfNull(adaptation);
        var glossary = adaptation.Glossary.Select(term => $"{term.Term}: {term.Definition}");
        return string.Join('\n', glossary.Prepend(adaptation.PlainText));
    }
}
