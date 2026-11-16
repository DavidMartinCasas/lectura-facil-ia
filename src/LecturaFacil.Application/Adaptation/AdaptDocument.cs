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
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Adaptation;

/// <summary>
/// Adapta un documento a lectura fácil con el modelo adaptador: "Recibido" → "Procesando" → "Borrador", o "Error" con
/// un motivo genérico. Un texto largo se adapta por partes (<see cref="TextChunker"/>) y se une. Cada parte tiene un
/// solo reintento si la respuesta no cumple el esquema; si el proveedor falla, no se reintenta aquí.
/// Después se revisan las pautas de la adaptación (<see cref="ReviewAdaptation"/>). Si incumple pautas de gravedad alta,
/// se pide otra adaptación (como mucho <see cref="LimitsOptions.MaxAdaptationRetries"/>) indicando qué pautas incumplió,
/// y se guarda la mejor según <see cref="BestVersionPolicy"/>. Si el reintento falla, se guarda la primera.
/// Por último se verifica el significado de la versión que se guarda (<see cref="VerifyMeaning"/>): solo de esa, para
/// no multiplicar las llamadas al verificador. La verificación nunca impide guardar el borrador; solo añade avisos.
/// Cada ejecución, bien o mal, se registra una vez en <see cref="IAdaptationTelemetry"/> (US-17).
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
        await repository.SaveAsync(document, cancellationToken);

        var adaptation = await AdaptAndReviewAsync(document, usage, cancellationToken);
        if (adaptation.IsFailure)
        {
            document.FailProcessing(adaptation.Error!.Code, timeProvider.GetUtcNow());
            await repository.SaveAsync(document, cancellationToken);
            return Result<AdaptedVersion>.Failure(adaptation.Error);
        }

        var (content, metadata, report) = adaptation.Value;
        var verification = await verifyMeaning.ExecuteAsync(document.OriginalText.Value, content, cancellationToken);
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

    /// <summary>Adapta, revisa las pautas y, si incumple alguna grave, reintenta y se queda con la mejor versión.</summary>
    private async Task<Result<ReviewedAdaptation>> AdaptAndReviewAsync(Document document, Usage total, CancellationToken cancellationToken)
    {
        var first = await AdaptAsync(document, SystemPrompt(document.OriginalFindings), new Usage(total), cancellationToken);
        if (first.IsFailure)
        {
            return Result<ReviewedAdaptation>.Failure(first.Error!);
        }

        var best = Review(first.Value);
        var last = best;
        var cost = best.Metadata;
        for (var retry = 0; retry < _limits.MaxAdaptationRetries && last.Report.HasHighSeverityViolations; retry++)
        {
            // Lo que cuestan las respuestas válidas del reintento se suma aunque el reintento acabe fallando en otra parte.
            var retryUsage = new Usage(total);
            var attempt = await AdaptAsync(document, RetrySystemPrompt(document.OriginalFindings, last.Report), retryUsage, cancellationToken);
            cost = AddCost(cost, retryUsage);
            if (attempt.IsFailure)
            {
                // La versión que ya hay es válida: un reintento fallido no tira el trabajo hecho.
                break;
            }

            last = Review(attempt.Value);
            if (BestVersionPolicy.PrefersRetry(best.Content, best.Report, last.Content, last.Report))
            {
                best = last;
            }
        }

        // La versión guardada indica el prompt que la generó y lo que han costado todos los intentos.
        var metadata = best.Metadata with
        {
            InputTokens = cost.InputTokens,
            OutputTokens = cost.OutputTokens,
            Duration = cost.Duration,
        };
        return Result<ReviewedAdaptation>.Success(best with { Metadata = metadata });
    }

    private ReviewedAdaptation Review((AdaptedContent Content, AdaptationMetadata Metadata) adaptation) =>
        new(adaptation.Content, adaptation.Metadata, reviewAdaptation.Execute(adaptation.Content));

    private static AdaptationMetadata AddCost(AdaptationMetadata total, Usage attempt) =>
        total with
        {
            InputTokens = total.InputTokens + attempt.InputTokens,
            OutputTokens = total.OutputTokens + attempt.OutputTokens,
            Duration = total.Duration + attempt.Duration,
        };

    /// <param name="usage">Acumula lo que cuestan las respuestas válidas, también si el intento acaba fallando.</param>
    private async Task<Result<(AdaptedContent Content, AdaptationMetadata Metadata)>> AdaptAsync(
        Document document,
        Prompt systemPrompt,
        Usage usage,
        CancellationToken cancellationToken)
    {
        var maxChunk = Math.Min(_limits.MaxCharactersPerAdaptationChunk, _limits.MaxCharacters);
        var parts = new List<AdaptedContent>();
        var chunks = TextChunker.Split(document.OriginalText.Value, maxChunk);
        if (chunks.Count > _limits.MaxAdaptationChunksPerDocument)
        {
            return Result<(AdaptedContent, AdaptationMetadata)>.Failure(LlmErrors.InputTooLong);
        }

        foreach (var chunk in chunks)
        {
            var part = await AdaptChunkAsync(systemPrompt, chunk, usage, cancellationToken);
            if (part.IsFailure)
            {
                return Result<(AdaptedContent, AdaptationMetadata)>.Failure(part.Error!);
            }

            parts.Add(part.Value);
        }

        var metadata = new AdaptationMetadata(
            usage.Provider, usage.Model, systemPrompt.Name, usage.InputTokens, usage.OutputTokens, usage.Duration);
        return Result<(AdaptedContent, AdaptationMetadata)>.Success((AdaptedContent.Merge(parts), metadata));
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

    /// <summary>Una adaptación con su informe de pautas.</summary>
    private sealed record ReviewedAdaptation(AdaptedContent Content, AdaptationMetadata Metadata, GuidelineReport Report);

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
