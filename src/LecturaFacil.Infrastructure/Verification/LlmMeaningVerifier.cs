// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;
using LecturaFacil.Application.Verification;
using LecturaFacil.Domain.Verification;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Verification;

/// <summary>
/// Verificador del significado con el rol <see cref="LlmRole.Verifier"/>, que se configura con un modelo de otra familia
/// que el adaptador (ADR 0002). El original y la adaptación van cada uno entre sus delimitadores (OWASP LLM01) y la
/// respuesta se valida contra su esquema antes de usarla. Un solo intento: si falla, <see cref="VerifyMeaning"/> deja
/// todo para revisar. Nunca registra los textos.
/// </summary>
public sealed class LlmMeaningVerifier(ILlmProvider llm, IPromptLoader prompts, IOptions<LimitsOptions> limits) : IMeaningVerifier
{
    /// <summary>Prompt de sistema del verificador. Se guarda con cada verificación para saber cuál la generó.</summary>
    public const string PromptName = "verify.v1";

    public async Task<Result<VerifierAssessment>> AssessAsync(string originalText, string adaptedText, CancellationToken cancellationToken)
    {
        var prompt = prompts.Load(PromptName);
        var request = LlmRequest.ForComparison(LlmRole.Verifier, prompt, originalText, adaptedText, limits.Value.MaxOutputTokens);
        var response = await llm.CompleteStructuredAsync<VerificationReply>(request, cancellationToken);
        if (response.IsFailure)
        {
            return Result<VerifierAssessment>.Failure(response.Error!);
        }

        var facts = response.Value.Value.KeyFacts
            .Where(fact => !string.IsNullOrWhiteSpace(fact.Original))
            .Select(ToKeyFact)
            .ToList()
            .AsReadOnly();
        var metadata = new VerificationMetadata(response.Value.Provider, response.Value.Model, prompt.Name);
        return Result<VerifierAssessment>.Success(new VerifierAssessment(facts, metadata));
    }

    private static KeyFact ToKeyFact(VerificationReplyFact fact) =>
        KeyFact.Create(
            ToType(fact.Type),
            Truncate(fact.Original)!,
            Truncate(fact.Adapted),
            KeyFactVerdict.Create(ToStatus(fact.Status), ToReason(fact.Reason)));

    /// <summary>Los fragmentos son citas para la persona que revisa; uno demasiado largo se corta.</summary>
    private static string? Truncate(string? text)
    {
        var trimmed = text?.Trim();
        return trimmed is { Length: > KeyFact.ValueMaxLength } ? trimmed[..KeyFact.ValueMaxLength] : trimmed;
    }

    private static KeyFactType ToType(VerificationReplyType type) => type switch
    {
        VerificationReplyType.Deadline => KeyFactType.Deadline,
        VerificationReplyType.Amount => KeyFactType.Amount,
        VerificationReplyType.Requirement => KeyFactType.Requirement,
        VerificationReplyType.Date => KeyFactType.Date,
        VerificationReplyType.Place => KeyFactType.Place,
        VerificationReplyType.Document => KeyFactType.Document,
        VerificationReplyType.Reference => KeyFactType.Reference,
        _ => KeyFactType.Other,
    };

    /// <summary>Un estado desconocido nunca cuenta como conservado.</summary>
    private static KeyFactStatus ToStatus(VerificationReplyStatus status) => status switch
    {
        VerificationReplyStatus.Conserved => KeyFactStatus.Conserved,
        VerificationReplyStatus.Omitted => KeyFactStatus.Omitted,
        _ => KeyFactStatus.Review,
    };

    private static KeyFactReason ToReason(VerificationReplyReason reason) => reason switch
    {
        VerificationReplyReason.None => KeyFactReason.None,
        VerificationReplyReason.ValueChanged => KeyFactReason.ValueChanged,
        VerificationReplyReason.CalculatedConversion => KeyFactReason.CalculatedConversion,
        VerificationReplyReason.NotInAdaptation => KeyFactReason.NotInAdaptation,
        _ => KeyFactReason.NeedsReview,
    };
}
