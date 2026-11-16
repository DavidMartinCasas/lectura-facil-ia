// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;
using LecturaFacil.Evals.Cli;
using LecturaFacil.Evals.Configuration;
using LecturaFacil.Evals.Dataset;
using LecturaFacil.Evals.DryRun;
using LecturaFacil.Evals.Metrics;
using LecturaFacil.Evals.Reporting;
using LecturaFacil.Evals.Similarity;
using LecturaFacil.Evals.Traps;
using LecturaFacil.Infrastructure.Llm;
using LecturaFacil.Infrastructure.Verification;
using LecturaFacil.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Evals.Running;

/// <summary>
/// Ejecuta la evaluación: lee el dataset y los perfiles, genera los textos trampa, pide confirmación del coste si hay
/// modelos de pago, evalúa cada perfil y escribe <c>informe.md</c> y <c>resultados.csv</c> en
/// <c>&lt;salida&gt;/&lt;fecha&gt;</c> (<c>&lt;fecha&gt;-dry-run</c> en simulación). Nunca escribe en la salida el
/// texto de los documentos ni las claves.
/// </summary>
public sealed class EvalRunner(TextReader input, TextWriter output, TimeProvider timeProvider, Func<string, string?> environment)
{
    public const int Success = 0;
    public const int InvalidInput = 1;
    public const int Cancelled = 2;

    /// <summary>Textos trampa como mucho (sección 18 del documento de diseño).</summary>
    public const int MaxTraps = 5;

    public const string ReportFileName = "informe.md";
    public const string CsvFileName = "resultados.csv";

    private static readonly string[] PromptNames = [AdaptDocument.PromptName, AdaptDocument.RetryPromptName, LlmMeaningVerifier.PromptName];
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly string[] AffirmativeAnswers = ["s", "si", "sí", "y", "yes"];

    public async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var parsed = EvalArguments.Parse(args);
        if (parsed.IsFailure)
        {
            return Fail(parsed.Error!, EvalArguments.Usage);
        }

        if (parsed.Value.Help)
        {
            await output.WriteLineAsync(EvalArguments.Usage);
            return Success;
        }

        var arguments = parsed.Value;
        var texts = DatasetLoader.Load(arguments.DatasetDirectory);
        if (texts.IsFailure)
        {
            return Fail(texts.Error!);
        }

        var configuration = arguments.DryRun
            ? Result<EvalConfiguration>.Success(DryRunConfiguration(arguments.Models))
            : EvalConfigurationLoader.Load(arguments.ConfigPath, arguments.Models, environment);
        if (configuration.IsFailure)
        {
            return Fail(configuration.Error!);
        }

        var limits = new LimitsOptions();
        var prompts = new EmbeddedPromptLoader();
        var traps = TrapGenerator.Generate(texts.Value, MaxTraps);
        if (!arguments.DryRun && !arguments.Yes && !await ConfirmCostAsync(configuration.Value, texts.Value, traps, limits, prompts))
        {
            await output.WriteLineAsync("Evaluación cancelada.");
            return Cancelled;
        }

        var embeddings = CreateEmbeddings(arguments.DryRun, configuration.Value.Embeddings, limits);
        using var disposableEmbeddings = embeddings as IDisposable;
        var evaluations = new List<ModelEvaluation>();
        foreach (var profile in configuration.Value.Models)
        {
            await output.WriteLineAsync($"Evaluando {profile.Name} ({texts.Value.Count} textos y {traps.Count} trampas)…");
            evaluations.Add(await EvaluateAsync(profile, arguments.DryRun, limits, embeddings, texts.Value, traps, cancellationToken));
        }

        var run = new RunInfo(
            timeProvider.GetUtcNow(),
            arguments.DryRun,
            configuration.Value.Currency,
            embeddings?.Model ?? "sin configurar (no se mide la similitud)",
            [.. PromptNames.Select(name => new PromptInfo(name, Sha256(prompts.Load(name).Text)))],
            limits,
            texts.Value);
        var directory = await WriteAsync(arguments.OutputDirectory, run, evaluations, cancellationToken);
        await output.WriteLineAsync($"Informe: {Path.Combine(directory, ReportFileName)}");
        return Success;
    }

    private async Task<ModelEvaluation> EvaluateAsync(
        ModelProfile profile,
        bool dryRun,
        LimitsOptions limits,
        IEmbeddingProvider? embeddings,
        IReadOnlyList<EvalText> texts,
        IReadOnlyList<Trap> traps,
        CancellationToken cancellationToken)
    {
        await using var services = EvaluationServices.Build(limits, provider => dryRun
            ? new FakeLlmProvider(DryRunResponder.ProviderName, DryRunResponder.Respond)
            : provider.GetRequiredService<LlmProviderFactory>().Create(profile.Provider, LlmProviderFactory.PrimaryHttpClient));
        var evaluator = new ProfileEvaluator(profile, services, embeddings, timeProvider);
        return await evaluator.EvaluateAsync(texts, traps, cancellationToken);
    }

    /// <summary>Límite de gasto: con modelos de pago, muestra la cota del coste y solo sigue si la persona contesta que sí.</summary>
    private async Task<bool> ConfirmCostAsync(
        EvalConfiguration configuration,
        IReadOnlyList<EvalText> texts,
        IReadOnlyList<Trap> traps,
        LimitsOptions limits,
        IPromptLoader prompts)
    {
        var paid = configuration.Models.Where(profile => !profile.Pricing.IsFree).ToList();
        if (paid.Count == 0)
        {
            return true;
        }

        var adapterPrompt = prompts.Load(AdaptDocument.PromptName).Text.Length + prompts.Load(AdaptDocument.RetryPromptName).Text.Length;
        var verifierPrompt = prompts.Load(LlmMeaningVerifier.PromptName).Text.Length;
        foreach (var profile in paid)
        {
            var estimate = CostEstimator.Estimate(
                texts.Select(text => text.Original.Length),
                traps.Select(trap => trap.Source.Original.Length + trap.AlteredAdaptation.Length),
                adapterPrompt,
                verifierPrompt,
                limits,
                profile.Pricing);
            await output.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"{profile.Name}: coste estimado como mucho {estimate:0.0000} {configuration.Currency}."));
        }

        await output.WriteAsync("¿Continuar? (s/N) ");
        var answer = (await input.ReadLineAsync())?.Trim().ToLowerInvariant();
        return answer is not null && AffirmativeAnswers.Contains(answer, StringComparer.Ordinal);
    }

    private async Task<string> WriteAsync(
        string outputDirectory,
        RunInfo run,
        IReadOnlyList<ModelEvaluation> evaluations,
        CancellationToken cancellationToken)
    {
        var name = run.StartedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + (run.DryRun ? "-dry-run" : string.Empty);
        var directory = Directory.CreateDirectory(Path.Combine(outputDirectory, name)).FullName;
        await File.WriteAllTextAsync(Path.Combine(directory, ReportFileName), MarkdownReport.Render(run, evaluations), Utf8, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, CsvFileName), CsvReport.Render(evaluations), Utf8, cancellationToken);
        return directory;
    }

    private int Fail(Error error, string? usage = null)
    {
        output.WriteLine($"Error: {error.Message}");
        if (usage is not null)
        {
            output.WriteLine(usage);
        }

        return InvalidInput;
    }

    private static EvalConfiguration DryRunConfiguration(IReadOnlyList<string> models) =>
        new(
            [.. models.Select(name => new ModelProfile(
                name,
                new LlmProviderOptions
                {
                    Type = LlmProviderType.OpenAICompatible,
                    DisplayName = DryRunResponder.ProviderName,
                    AdapterModel = DryRunResponder.AdapterModel,
                    VerifierModel = DryRunResponder.VerifierModel,
                },
                ModelPricing.Free))],
            null,
            "USD");

    private static IEmbeddingProvider? CreateEmbeddings(bool dryRun, EmbeddingSettings? settings, LimitsOptions limits)
    {
        if (dryRun)
        {
            return new LexicalEmbeddingProvider();
        }

        return settings is null ? null : new OpenAIEmbeddingProvider(settings, TimeSpan.FromSeconds(limits.LlmTimeoutSeconds));
    }

    private static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
