// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Text;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Llm;
using LecturaFacil.Application.Verification;
using LecturaFacil.Domain.Adaptation;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Adaptation;

/// <summary>
/// Adapta un documento a lectura fácil con el modelo adaptador: "Recibido" → "Procesando" → "Borrador", o "Error" con
/// un motivo genérico. Un texto largo se adapta por partes (<see cref="TextChunker"/>) y se une, pero se recuerda qué
/// parte adaptada salió de qué parte del original (US-23). Cada parte tiene un solo reintento si la respuesta no cumple
/// el esquema; si el proveedor falla, no se reintenta aquí.
/// Después se revisan las pautas de cada parte (<see cref="ReviewAdaptation"/>). Si una parte incumple pautas de gravedad
/// alta, se pide otra adaptación solo de esa parte (como mucho <see cref="LimitsOptions.MaxAdaptationRetries"/>)
/// indicando qué pautas incumplió, y de cada parte se guarda la mejor según <see cref="BestVersionPolicy"/>. Si el
/// reintento de una parte falla, se queda la versión que ya tenía.
/// Por último se verifica el significado de la versión que se guarda (<see cref="VerifyMeaning"/>), parte a parte: solo
/// de esa versión, para no multiplicar las llamadas al verificador. La verificación nunca impide guardar el borrador;
/// solo añade avisos. Como mucho hay partes × (1 + reintentos) llamadas al adaptador (más el reintento por esquema de
/// cada una) y tantas llamadas al verificador como partes.
/// Cada ejecución, bien o mal, se registra una vez en <see cref="IAdaptationTelemetry"/> (US-17).
/// Mientras dura, se guarda en qué fase está (<see cref="ProcessingStage"/>) y, con varias partes, por cuál va, para que la
/// pantalla enseñe el progreso real.
/// </summary>
public sealed class AdaptDocument(
    IDocumentRepository repository,
    ILlmProvider llm,
    IPromptLoader prompts,
    ReviewAdaptation reviewAdaptation,
    VerifyMeaning verifyMeaning,
    IOptions<LimitsOptions> limits,
    IAdaptationTelemetry telemetry,
    TimeProvider timeProvider)
{
    /// <summary>Prompt de sistema del adaptador. Se guarda con cada versión para saber cuál la generó.</summary>
    public const string PromptName = "adapter.v1";

    /// <summary>Instrucciones que se añaden al prompt del adaptador cuando se pide otra adaptación por incumplir pautas.</summary>
    public const string RetryPromptName = "adapter-retry.v1";

    /// <summary>Versión de prompt que se guarda con una versión que sale de un reintento.</summary>
    public const string RetryPromptVersion = $"{PromptName}+{RetryPromptName}";

    /// <summary>Marcador del prompt donde van las pautas que incumple el original (texto del catálogo, nunca del usuario).</summary>
    public const string GuidelinesPlaceholder = "{{PAUTAS_DETECTADAS}}";

    /// <summary>Marcador del prompt para <see cref="LimitsOptions.MaxWordsPerSentence"/>, el mismo umbral que usa el revisor de pautas.</summary>
    public const string MaxWordsPlaceholder = "{{MAX_PALABRAS_POR_FRASE}}";

    /// <summary>Marcador del prompt de reintento donde van las pautas que incumplió la adaptación (texto del catálogo).</summary>
    public const string RetryPlaceholder = "{{PAUTAS_INCUMPLIDAS}}";

    /// <summary>Marcador del prompt de reintento para <see cref="LimitsOptions.MaxWordsPerHeading"/>.</summary>
    public const string MaxHeadingWordsPlaceholder = "{{MAX_PALABRAS_POR_TITULO}}";

    public const string NoFindingsLine = "- No se ha detectado ningún problema concreto. Aplica igualmente todas las pautas.";

    /// <summary>Un intento y un solo reintento si la respuesta no es válida (US-06, OWASP LLM10).</summary>
    public const int MaxAttemptsPerChunk = 2;

    private readonly LimitsOptions _limits = limits.Value;

    public async Task<Result<AdaptedVersion>> ExecuteAsync(DocumentId id, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        var usage = new Usage();
        var result = await RunAsync(id, usage, cancellationToken);
        telemetry.Record(new AdaptationOutcome(
            id,
            usage.Role,
            usage.Role is null ? null : usage.Provider,
            usage.Role is null ? null : usage.Model,
            usage.InputTokens,
            usage.OutputTokens,
            timeProvider.GetElapsedTime(started),
            result.IsSuccess ? AdaptationOutcome.SuccessResult : result.Error!.Code,
            usage.TokensByRole));
        return result;
    }

    /// <param name="usage">Acumula lo que cuestan todas las respuestas válidas de esta ejecución, para la telemetría.</param>
    private async Task<Result<AdaptedVersion>> RunAsync(DocumentId id, Usage usage, CancellationToken cancellationToken)
    {
        var document = await repository.FindAsync(id, cancellationToken);
        if (document is null)
        {
            return Result<AdaptedVersion>.Failure(DocumentErrors.NotFound);
        }

        if (!DocumentStatusTransitions.IsAwaitingAdaptation(document.Status))
        {
            return Result<AdaptedVersion>.Failure(AdaptationErrors.NotAwaitingAdaptation);
        }

        document.StartProcessing(timeProvider.GetUtcNow());
        var chunks = TextChunker.Split(document.OriginalText.Value, Math.Min(_limits.MaxCharactersPerAdaptationChunk, _limits.MaxCharacters));
        if (chunks.Count > _limits.MaxAdaptationChunksPerDocument)
        {
            document.FailProcessing(LlmErrors.InputTooLong.Code, timeProvider.GetUtcNow());
            await repository.SaveAsync(document, cancellationToken);
            return Result<AdaptedVersion>.Failure(LlmErrors.InputTooLong);
        }

        var adaptation = await AdaptAndReviewAsync(document, chunks, usage, cancellationToken);
        if (adaptation.IsFailure)
        {
            document.FailProcessing(adaptation.Error!.Code, timeProvider.GetUtcNow());
            await repository.SaveAsync(document, cancellationToken);
            return Result<AdaptedVersion>.Failure(adaptation.Error);
        }

        var (parts, content, metadata, report) = adaptation.Value;
        var verification = await VerifyAsync(document, chunks, parts, content, cancellationToken);
        var version = document.CompleteAdaptation(content, metadata, report, verification, timeProvider.GetUtcNow());
        await repository.SaveAsync(document, cancellationToken);
        return Result<AdaptedVersion>.Success(version);
    }

    /// <summary>
    /// Tras un fallo inesperado (una excepción, no un error esperado), deja el documento en "Error" con un motivo
    /// genérico para que no se quede en "Procesando" hasta el siguiente reinicio.
    /// </summary>
    public async Task FailUnexpectedlyAsync(DocumentId id, CancellationToken cancellationToken)
    {
        var document = await repository.FindAsync(id, cancellationToken);
        if (document is not { Status: DocumentStatus.Processing })
        {
            return;
        }

        document.FailProcessing(AdaptationErrors.Failed.Code, timeProvider.GetUtcNow());
        await repository.SaveAsync(document, cancellationToken);
    }

    /// <summary>
    /// Adapta cada parte, revisa sus pautas y reintenta solo las partes que incumplen alguna grave, quedándose con la
    /// mejor versión de cada una. Si ninguna parte incumple nada grave pero el documento unido sí (una pauta que solo se
    /// ve en el conjunto), se reintentan todas, como antes de ir por partes.
    /// </summary>
    private async Task<Result<ReviewedAdaptation>> AdaptAndReviewAsync(
        Document document,
        IReadOnlyList<string> chunks,
        Usage total,
        CancellationToken cancellationToken)
    {
        // Lo que cuestan las respuestas válidas se suma aunque un intento acabe fallando en otra parte.
        var adapterUsage = new Usage(total);
        var prompt = SystemPrompt(document.OriginalFindings);
        var best = new ReviewedPart[chunks.Count];
        for (var index = 0; index < chunks.Count; index++)
        {
            await EnterStageAsync(document, ProcessingStage.Adapting, index, chunks.Count, cancellationToken);
            var part = await AdaptChunkAsync(prompt, chunks[index], adapterUsage, cancellationToken);
            if (part.IsFailure)
            {
                return Result<ReviewedAdaptation>.Failure(part.Error!);
            }

            best[index] = new ReviewedPart(part.Value, reviewAdaptation.Execute(part.Value), FromRetry: false);
        }

        await EnterStageAsync(document, ProcessingStage.ReviewingGuidelines, cancellationToken);
        var last = (ReviewedPart[])best.Clone();
        var exhausted = new bool[chunks.Count];
        for (var retry = 0; retry < _limits.MaxAdaptationRetries; retry++)
        {
            var toRetry = PartsToRetry(last, exhausted);
            if (toRetry.Count == 0)
            {
                break;
            }

            foreach (var (index, report) in toRetry)
            {
                await EnterStageAsync(document, ProcessingStage.Retrying, index, chunks.Count, cancellationToken);
                var attempt = await AdaptChunkAsync(
                    RetrySystemPrompt(document.OriginalFindings, report), chunks[index], adapterUsage, cancellationToken);
                if (attempt.IsFailure)
                {
                    // La versión que ya tiene esta parte es válida: un reintento fallido no tira el trabajo hecho.
                    exhausted[index] = true;
                    continue;
                }

                last[index] = new ReviewedPart(attempt.Value, reviewAdaptation.Execute(attempt.Value), FromRetry: true);
                if (BestVersionPolicy.PrefersRetry(best[index].Content, best[index].Report, last[index].Content, last[index].Report))
                {
                    best[index] = last[index];
                }
            }

            await EnterStageAsync(document, ProcessingStage.ReviewingGuidelines, cancellationToken);
        }

        var content = AdaptedContent.Merge([.. best.Select(part => part.Content)]);
        // La versión guardada indica el prompt que la generó y lo que han costado todos los intentos.
        var metadata = new AdaptationMetadata(
            adapterUsage.Provider,
            adapterUsage.Model,
            best.Any(part => part.FromRetry) ? RetryPromptVersion : PromptName,
            adapterUsage.InputTokens,
            adapterUsage.OutputTokens,
            adapterUsage.Duration);
        return Result<ReviewedAdaptation>.Success(
            new ReviewedAdaptation([.. best.Select(part => part.Content)], content, metadata, reviewAdaptation.Execute(content)));
    }

    /// <summary>
    /// Las partes que hay que volver a adaptar, con las pautas que incumplieron: las que incumplen alguna grave y, si
    /// ninguna lo hace pero el documento unido sí, todas con el informe del documento.
    /// </summary>
    private List<(int Index, GuidelineReport Report)> PartsToRetry(ReviewedPart[] last, bool[] exhausted)
    {
        var failing = last
            .Select((part, index) => (Index: index, part.Report))
            .Where(part => !exhausted[part.Index] && part.Report.HasHighSeverityViolations)
            .ToList();
        if (last.Any(part => part.Report.HasHighSeverityViolations))
        {
            return failing;
        }

        var merged = reviewAdaptation.Execute(AdaptedContent.Merge([.. last.Select(part => part.Content)]));
        return merged.HasHighSeverityViolations
            ? [.. Enumerable.Range(0, last.Length).Where(index => !exhausted[index]).Select(index => (index, merged))]
            : [];
    }

    /// <summary>Verifica la versión que se guarda, una llamada por parte, y enseña por cuál va.</summary>
    private async Task<MeaningVerification> VerifyAsync(
        Document document,
        IReadOnlyList<string> chunks,
        IReadOnlyList<AdaptedContent> parts,
        AdaptedContent content,
        CancellationToken cancellationToken)
    {
        var toVerify = chunks.Select((chunk, index) => new PartToVerify(chunk, parts[index])).ToList();
        if (toVerify.Count == 1)
        {
            await EnterStageAsync(document, ProcessingStage.Verifying, cancellationToken);
            return await verifyMeaning.ExecuteAsync(toVerify, content, onPart: null, cancellationToken);
        }

        return await verifyMeaning.ExecuteAsync(
            toVerify,
            content,
            (part, total, token) => EnterStageAsync(document, ProcessingStage.Verifying, part - 1, total, token),
            cancellationToken);
    }

    /// <summary>Guarda la fase en la que entra el documento: el seguimiento por SSE la lee y la envía a la pantalla.</summary>
    private async Task EnterStageAsync(Document document, ProcessingStage stage, CancellationToken cancellationToken)
    {
        document.EnterStage(stage, timeProvider.GetUtcNow());
        await repository.SaveAsync(document, cancellationToken);
    }

    /// <summary>
    /// Igual, diciendo por qué parte va (<paramref name="index"/> desde 0). Con una sola parte no se guarda el número:
    /// la pantalla no tiene nada que contar.
    /// </summary>
    private async Task EnterStageAsync(Document document, ProcessingStage stage, int index, int parts, CancellationToken cancellationToken)
    {
        if (parts == 1)
        {
            document.EnterStage(stage, timeProvider.GetUtcNow());
        }
        else
        {
            document.EnterStage(stage, timeProvider.GetUtcNow(), index + 1, parts);
        }

        await repository.SaveAsync(document, cancellationToken);
    }

    private async Task<Result<AdaptedContent>> AdaptChunkAsync(
        Prompt systemPrompt,
        string chunk,
        Usage usage,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttemptsPerChunk; attempt++)
        {
            // Delimitadores nuevos en cada intento: el documento no puede conocerlos ni cerrarlos (OWASP LLM01).
            var request = LlmRequest.ForDocument(LlmRole.Adapter, systemPrompt, chunk, _limits.MaxOutputTokens);
            var response = await llm.CompleteStructuredAsync<AdaptationReply>(request, cancellationToken);
            if (response.IsFailure && response.Error != LlmErrors.InvalidProviderOutput)
            {
                return Result<AdaptedContent>.Failure(response.Error!);
            }

            if (response.IsSuccess)
            {
                usage.Add(response.Value);
                if (ToContent(response.Value.Value) is { } content)
                {
                    return Result<AdaptedContent>.Success(content);
                }
            }
        }

        return Result<AdaptedContent>.Failure(LlmErrors.InvalidProviderOutput);
    }

    /// <summary>El esquema garantiza la forma; aquí se comprueba que nada importante esté vacío.</summary>
    private static AdaptedContent? ToContent(AdaptationReply reply) =>
        AdaptedContent.TryCreate(
            reply.Title,
            reply.Sections.Select(section => new AdaptedSectionDraft(section.Question, section.Sentences)),
            reply.Glossary.Select(term => new GlossaryTermDraft(term.Term, term.Definition)));

    /// <summary>
    /// El prompt versionado con las pautas que incumple el original y cuántas veces. Solo usa las descripciones del
    /// catálogo: ningún fragmento del documento sale del bloque delimitado.
    /// </summary>
    private Prompt SystemPrompt(IReadOnlyList<Finding> findings)
    {
        var prompt = prompts.Load(PromptName);
        var groups = findings
            .GroupBy(finding => finding.GuidelineCode, StringComparer.Ordinal)
            .Select(group => (Guideline: GuidelineCatalog.Find(group.Key), Count: group.Count()))
            .OrderByDescending(group => group.Guideline.DefaultSeverity)
            .ThenByDescending(group => group.Count);
        var lines = GuidelineLines(groups);
        var guidelines = lines.Length == 0 ? NoFindingsLine : lines;
        var text = prompt.Text
            .Replace(GuidelinesPlaceholder, guidelines, StringComparison.Ordinal)
            .Replace(MaxWordsPlaceholder, _limits.MaxWordsPerSentence.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        return prompt with { Text = text };
    }

    /// <summary>
    /// El prompt del adaptador más las pautas que incumplió la adaptación anterior. Igual que en el primero, solo lleva
    /// descripciones fijas del catálogo y números, nunca fragmentos del documento ni de la respuesta del modelo.
    /// </summary>
    private Prompt RetrySystemPrompt(IReadOnlyList<Finding> originalFindings, GuidelineReport report)
    {
        var basePrompt = SystemPrompt(originalFindings);
        var retry = prompts.Load(RetryPromptName);
        var violations = GuidelineLines(report.Violations.Select(result => (result.Guideline, result.FindingCount)));
        var instructions = retry.Text
            .Replace(RetryPlaceholder, violations, StringComparison.Ordinal)
            .Replace(MaxHeadingWordsPlaceholder, _limits.MaxWordsPerHeading.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        return new Prompt(RetryPromptVersion, $"{basePrompt.Text}\n\n{instructions}");
    }

    private static string GuidelineLines(IEnumerable<(Guideline Guideline, int Count)> groups)
    {
        var lines = new StringBuilder();
        foreach (var (guideline, count) in groups)
        {
            lines.AppendLine(CultureInfo.InvariantCulture, $"- {guideline.Description}: {count}");
        }

        return lines.ToString().TrimEnd();
    }

    /// <summary>La adaptación que se guarda: cada parte, todas unidas, lo que ha costado y su informe de pautas.</summary>
    private sealed record ReviewedAdaptation(
        IReadOnlyList<AdaptedContent> Parts,
        AdaptedContent Content,
        AdaptationMetadata Metadata,
        GuidelineReport Report);

    /// <summary>Una parte adaptada con su informe de pautas y si sale de un reintento.</summary>
    private sealed record ReviewedPart(AdaptedContent Content, GuidelineReport Report, bool FromRetry);

    /// <summary>
    /// Suma de lo que han costado las respuestas válidas de todas las partes. Si tiene <c>parent</c>, también se lo suma
    /// a él: así la telemetría ve el total de la ejecución, incluidos los intentos que se descartan.
    /// </summary>
    private sealed class Usage(Usage? parent = null)
    {
        public string Provider { get; private set; } = string.Empty;

        public string Model { get; private set; } = string.Empty;

        public LlmProviderRole? Role { get; private set; }

        public IReadOnlyList<RoleTokens> TokensByRole => [.. _byRole.Select(pair => new RoleTokens(pair.Key, pair.Value.Input, pair.Value.Output))];

        private readonly SortedDictionary<LlmProviderRole, (long Input, long Output)> _byRole = [];

        public long InputTokens { get; private set; }

        public long OutputTokens { get; private set; }

        public TimeSpan Duration { get; private set; }

        public void Add<T>(LlmResponse<T> response)
        {
            parent?.Add(response);
            Role = response.Role;
            var (input, output) = _byRole.GetValueOrDefault(response.Role);
            _byRole[response.Role] = (input + response.InputTokens, output + response.OutputTokens);
            Provider = response.Provider;
            Model = response.Model;
            InputTokens += response.InputTokens;
            OutputTokens += response.OutputTokens;
            Duration += response.Duration;
        }
    }
}
