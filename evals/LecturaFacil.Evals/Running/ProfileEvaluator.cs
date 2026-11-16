// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Readability;
using LecturaFacil.Application.Verification;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Evals.Configuration;
using LecturaFacil.Evals.Dataset;
using LecturaFacil.Evals.Metrics;
using LecturaFacil.Evals.Reporting;
using LecturaFacil.Evals.Similarity;
using LecturaFacil.Evals.Traps;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Evals.Running;

/// <summary>
/// Evalúa un perfil con los mismos casos de uso que producción: cada texto se recibe con <see cref="SubmitDocument"/>
/// y se adapta con <see cref="AdaptDocument"/> (pautas, reintento y verificación incluidos); la legibilidad se mide con
/// <see cref="CompareReadability"/>. Los textos trampa pasan solo por <see cref="VerifyMeaning"/>.
/// Un texto que falla queda con su código de error y la evaluación sigue con el siguiente.
/// </summary>
internal sealed class ProfileEvaluator(
    ModelProfile profile,
    IServiceProvider services,
    IEmbeddingProvider? embeddings,
    TimeProvider timeProvider)
{
    private readonly MeteredLlmProvider _meter = services.GetRequiredService<MeteredLlmProvider>();

    public async Task<ModelEvaluation> EvaluateAsync(
        IReadOnlyList<EvalText> texts,
        IReadOnlyList<Trap> traps,
        CancellationToken cancellationToken)
    {
        var textResults = new List<TextResult>();
        foreach (var text in texts)
        {
            textResults.Add(await EvaluateTextAsync(text, cancellationToken));
        }

        var trapResults = new List<TrapResult>();
        foreach (var trap in traps)
        {
            trapResults.Add(await EvaluateTrapAsync(trap, cancellationToken));
        }

        return new ModelEvaluation(profile, textResults.AsReadOnly(), trapResults.AsReadOnly());
    }

    private async Task<TextResult> EvaluateTextAsync(EvalText text, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        _meter.TakeUsage();
        var started = timeProvider.GetTimestamp();

        var submitted = await scope.ServiceProvider.GetRequiredService<SubmitDocument>()
            .SubmitTextAsync(new SubmitTextCommand(text.Original, text.Id), cancellationToken);
        if (submitted.IsFailure)
        {
            return Failed(text, submitted.Error!.Code, started);
        }

        var adapted = await scope.ServiceProvider.GetRequiredService<AdaptDocument>()
            .ExecuteAsync(submitted.Value.Id, cancellationToken);
        if (adapted.IsFailure)
        {
            return Failed(text, adapted.Error!.Code, started);
        }

        var version = adapted.Value;
        var duration = timeProvider.GetElapsedTime(started);
        var usage = _meter.TakeUsage();
        var readability = scope.ServiceProvider.GetRequiredService<CompareReadability>().Compare(text.Original, version.PlainText);
        var similarity = await SimilarityAsync(version.PlainText, text.ProfessionalAdaptation, cancellationToken);
        return new TextResult(
            text.Id,
            null,
            EvalMetrics.GuidelineCompliance(version.GuidelineReport),
            readability.IsSuccess ? readability.Value.Original.Value : null,
            readability.IsSuccess ? readability.Value.Adapted.Value : null,
            EvalMetrics.KeyFactRetention(version.MeaningVerification),
            EvalMetrics.KeyFactWarnings(version.MeaningVerification),
            similarity,
            duration,
            usage.InputTokens,
            usage.OutputTokens,
            usage.Duration,
            EvalMetrics.Cost(usage.InputTokens, usage.OutputTokens, profile.Pricing),
            version.Metadata.Model,
            version.Metadata.PromptVersion);
    }

    private async Task<TrapResult> EvaluateTrapAsync(Trap trap, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        _meter.TakeUsage();
        var content = ProfessionalAdaptation.ToContent(trap.AlteredAdaptation);
        var verification = await scope.ServiceProvider.GetRequiredService<VerifyMeaning>()
            .ExecuteAsync(trap.Source.Original, content, cancellationToken);
        var usage = _meter.TakeUsage();
        return new TrapResult(
            trap.Source.Id,
            trap.OriginalFigure,
            trap.AlteredFigure,
            TrapDetection.IsDetected(verification, trap),
            usage.InputTokens,
            usage.OutputTokens,
            EvalMetrics.Cost(usage.InputTokens, usage.OutputTokens, profile.Pricing));
    }

    /// <summary><c>null</c> si no hay modelo de embeddings configurado.</summary>
    private async Task<double?> SimilarityAsync(string adapted, string professional, CancellationToken cancellationToken)
    {
        if (embeddings is null)
        {
            return null;
        }

        var first = await embeddings.EmbedAsync(adapted, cancellationToken);
        var second = await embeddings.EmbedAsync(professional, cancellationToken);
        return EvalMetrics.CosineSimilarity(first, second);
    }

    private TextResult Failed(EvalText text, string errorCode, long started)
    {
        var usage = _meter.TakeUsage();
        return new TextResult(
            text.Id,
            errorCode,
            null,
            null,
            null,
            null,
            0,
            null,
            timeProvider.GetElapsedTime(started),
            usage.InputTokens,
            usage.OutputTokens,
            usage.Duration,
            EvalMetrics.Cost(usage.InputTokens, usage.OutputTokens, profile.Pricing));
    }
}
