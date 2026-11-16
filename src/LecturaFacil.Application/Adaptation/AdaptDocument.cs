// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Text;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Llm;
using LecturaFacil.Domain.Adaptation;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Adaptation;

/// <summary>
/// Adapta un documento a lectura fácil con el modelo adaptador: "Recibido" → "Procesando" → "Borrador", o "Error" con
/// un motivo genérico. Un texto largo se adapta por partes (<see cref="TextChunker"/>) y se une. Cada parte tiene un
/// solo reintento si la respuesta no cumple el esquema; si el proveedor falla, no se reintenta aquí.
/// </summary>
public sealed class AdaptDocument(
    IDocumentRepository repository,
    ILlmProvider llm,
    IPromptLoader prompts,
    IOptions<LimitsOptions> limits,
    TimeProvider timeProvider)
{
    /// <summary>Prompt de sistema del adaptador. Se guarda con cada versión para saber cuál la generó.</summary>
    public const string PromptName = "adapter.v1";

    /// <summary>Marcador del prompt donde van las pautas que incumple el original (texto del catálogo, nunca del usuario).</summary>
    public const string GuidelinesPlaceholder = "{{PAUTAS_DETECTADAS}}";

    /// <summary>Marcador del prompt para <see cref="LimitsOptions.MaxWordsPerSentence"/>, el mismo umbral que usa el revisor de pautas.</summary>
    public const string MaxWordsPlaceholder = "{{MAX_PALABRAS_POR_FRASE}}";

    public const string NoFindingsLine = "- No se ha detectado ningún problema concreto. Aplica igualmente todas las pautas.";

    /// <summary>Un intento y un solo reintento si la respuesta no es válida (US-06, OWASP LLM10).</summary>
    private const int MaxAttemptsPerChunk = 2;

    private readonly LimitsOptions _limits = limits.Value;

    public async Task<Result<AdaptedVersion>> ExecuteAsync(DocumentId id, CancellationToken cancellationToken)
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

        var adaptation = await AdaptAsync(document, cancellationToken);
        if (adaptation.IsFailure)
        {
            document.FailProcessing(adaptation.Error!.Code, timeProvider.GetUtcNow());
            await repository.SaveAsync(document, cancellationToken);
            return Result<AdaptedVersion>.Failure(adaptation.Error);
        }

        var (content, metadata) = adaptation.Value;
        var version = document.CompleteAdaptation(content, metadata, timeProvider.GetUtcNow());
        await repository.SaveAsync(document, cancellationToken);
        return Result<AdaptedVersion>.Success(version);
    }

    private async Task<Result<(AdaptedContent Content, AdaptationMetadata Metadata)>> AdaptAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var systemPrompt = SystemPrompt(document.OriginalFindings);
        var maxChunk = Math.Min(_limits.MaxCharactersPerAdaptationChunk, _limits.MaxCharacters);
        var parts = new List<AdaptedContent>();
        var usage = new Usage();
        foreach (var chunk in TextChunker.Split(document.OriginalText.Value, maxChunk))
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
        var lines = new StringBuilder();
        foreach (var group in findings.GroupBy(finding => finding.GuidelineCode, StringComparer.Ordinal).OrderByDescending(group => group.Count()))
        {
            var description = GuidelineCatalog.Find(group.Key).Description;
            lines.AppendLine(CultureInfo.InvariantCulture, $"- {description}: {group.Count()}");
        }

        var guidelines = lines.Length == 0 ? NoFindingsLine : lines.ToString().TrimEnd();
        var text = prompt.Text
            .Replace(GuidelinesPlaceholder, guidelines, StringComparison.Ordinal)
            .Replace(MaxWordsPlaceholder, _limits.MaxWordsPerSentence.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        return prompt with { Text = text };
    }

    /// <summary>Suma de lo que han costado las respuestas válidas de todas las partes.</summary>
    private sealed class Usage
    {
        public string Provider { get; private set; } = string.Empty;

        public string Model { get; private set; } = string.Empty;

        public long InputTokens { get; private set; }

        public long OutputTokens { get; private set; }

        public TimeSpan Duration { get; private set; }

        public void Add<T>(LlmResponse<T> response)
        {
            Provider = response.Provider;
            Model = response.Model;
            InputTokens += response.InputTokens;
            OutputTokens += response.OutputTokens;
            Duration += response.Duration;
        }
    }
}
