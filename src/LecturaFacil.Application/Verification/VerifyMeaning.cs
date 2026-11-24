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
/// puede añadir avisos. Un documento largo se verifica por partes, una llamada por cada parte del original con su parte
/// adaptada, y se unen los resultados (<see cref="MeaningVerification.CombineParts"/>, US-23). Nunca falla: si el
/// verificador no responde, su respuesta no es válida o una parte es demasiado larga, las cifras de esa parte quedan para
/// revisar (fallo seguro). No cambia la adaptación.
/// </summary>
public sealed class VerifyMeaning(IMeaningVerifier verifier, IOptions<LimitsOptions> limits)
{
    private readonly LimitsOptions _limits = limits.Value;

    /// <summary>Verifica un documento de una sola parte.</summary>
    public Task<MeaningVerification> ExecuteAsync(string originalText, AdaptedContent adaptation, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalText);
        ArgumentNullException.ThrowIfNull(adaptation);
        return ExecuteAsync([new PartToVerify(originalText, adaptation)], adaptation, onPart: null, cancellationToken);
    }

    /// <param name="parts">Cada parte del original con la parte adaptada que salió de ella, en orden.</param>
    /// <param name="adaptation">La adaptación completa (todas las partes unidas), la que leerá la persona.</param>
    /// <param name="onPart">Se avisa antes de verificar cada parte (número desde 1 y total), para enseñar el progreso.</param>
    public async Task<MeaningVerification> ExecuteAsync(
        IReadOnlyList<PartToVerify> parts,
        AdaptedContent adaptation,
        Func<int, int, CancellationToken, Task>? onPart,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(adaptation);
        if (parts.Count == 0)
        {
            throw new ArgumentException("There must be at least one part to verify.", nameof(parts));
        }

        try
        {
            var results = new List<VerificationPart>(parts.Count);
            for (var index = 0; index < parts.Count; index++)
            {
                if (onPart is not null)
                {
                    await onPart(index + 1, parts.Count, cancellationToken);
                }

                results.Add(await VerifyPartAsync(parts[index], cancellationToken));
            }

            return MeaningVerification.CombineParts(results, TextToVerify(adaptation), _limits.MaxKeyFactsPerVersion);
        }
        catch (RegexMatchTimeoutException)
        {
            // Un texto anómalo no debe tirar el borrador: se pide revisar todo el documento.
            return MeaningVerification.Unreadable();
        }
    }

    private async Task<VerificationPart> VerifyPartAsync(PartToVerify part, CancellationToken cancellationToken)
    {
        var adaptedText = TextToVerify(part.Adaptation);
        if (part.OriginalText.Length + adaptedText.Length > _limits.MaxCharactersPerVerification)
        {
            return VerificationPart.Failed(part.OriginalText);
        }

        var assessment = await verifier.AssessAsync(part.OriginalText, adaptedText, cancellationToken);
        return assessment.IsSuccess
            ? VerificationPart.Verified(part.OriginalText, assessment.Value.KeyFacts, assessment.Value.Metadata)
            : VerificationPart.Failed(part.OriginalText);
    }

    /// <summary>Todo lo que leerá la persona: el texto adaptado y, después, el glosario.</summary>
    public static string TextToVerify(AdaptedContent adaptation)
    {
        ArgumentNullException.ThrowIfNull(adaptation);
        var glossary = adaptation.Glossary.Select(term => $"{term.Term}: {term.Definition}");
        return string.Join('\n', glossary.Prepend(adaptation.PlainText));
    }
}

/// <summary>Una parte del original y la parte adaptada que salió de ella.</summary>
public sealed record PartToVerify(string OriginalText, AdaptedContent Adaptation);
