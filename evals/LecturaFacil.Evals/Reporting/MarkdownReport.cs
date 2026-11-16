// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Text;
using LecturaFacil.Evals.Traps;

namespace LecturaFacil.Evals.Reporting;

/// <summary>Informe en Markdown con las tablas de resultados y todo lo necesario para repetir la evaluación.</summary>
public static class MarkdownReport
{
    private const string NotMeasured = "n/d";
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    public static string Render(RunInfo run, IReadOnlyList<ModelEvaluation> evaluations)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(evaluations);

        var report = new StringBuilder();
        Line(report, $"# Evaluación de modelos · {run.StartedAt.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)}");
        Line(report);
        if (run.DryRun)
        {
            Line(report, "> **Simulación (`--dry-run`).** Ningún modelo real ha intervenido: estos números solo comprueban que la herramienta funciona y **no sirven para elegir modelo**.");
            Line(report);
        }

        Line(report, "Las adaptaciones son borradores: los datos clave con aviso y una muestra de los conservados los tiene que revisar una persona antes de dar por buenos los resultados.");
        Line(report);
        Summary(report, run, evaluations);
        Traps(report, evaluations);
        Details(report, evaluations);
        Configuration(report, run, evaluations);
        return report.ToString();
    }

    private static void Summary(StringBuilder report, RunInfo run, IReadOnlyList<ModelEvaluation> evaluations)
    {
        Line(report, "## Resultados por modelo");
        Line(report);
        Line(report, $"| Modelo | Textos adaptados | Pautas cumplidas | Δ INFLESZ | Datos clave conservados | Datos a revisar | Datos omitidos | Similitud | Tiempo medio | tok/s | Coste mínimo ({Cell(run.Currency)}) | Recall total | Recall del verificador LLM |");
        Line(report, "|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var summary in evaluations.Select(ModelSummary.From))
        {
            Line(report, $"| {Cell(summary.Name)} | {summary.SucceededTexts} de {summary.TotalTexts} | {Percent(summary.GuidelineCompliance)} | {Number(summary.InfleszDelta, "+0.0;-0.0;0.0")} | {Percent(summary.KeyFactRetention)} | {summary.KeyFactsToReview} | {summary.KeyFactsOmitted} | {Number(summary.Similarity, "0.000")} | {Seconds(summary.MeanDuration)} | {Number(summary.TokensPerSecond, "0.0")} | {Money(summary.Cost)} | {Percent(summary.Recall)} ({summary.TrapsDetected} de {summary.TrapsVerified}) | {Percent(summary.VerifierRecall)} ({summary.TrapsDetectedByVerifier} de {summary.TrapsVerified}){NotVerifiedNote(summary.TrapsNotVerified)} |");
        }

        Line(report);
        Line(report, "- **Pautas cumplidas:** media de pautas cumplidas según el revisor determinista, sobre las revisadas.");
        Line(report, "- **Δ INFLESZ:** INFLESZ de la adaptación menos el del original (más alto es más fácil).");
        Line(report, "- **Datos clave conservados:** conservados entre todos los que lista la verificación (verificador más comprobación de cifras por código). Cada perfil usa su propio verificador: para comparar adaptadores, configure el mismo verificador en todos.");
        Line(report, "- **Datos a revisar / omitidos:** avisos de la verificación. Los omitidos son los más graves; entre los de revisar están también las conversiones calculadas bien marcadas.");
        Line(report, "- **Similitud:** similitud coseno de los embeddings de la adaptación y de la adaptación profesional.");
        Line(report, "- **Coste mínimo:** tokens de las respuestas válidas (adaptación, reintentos, verificación y trampas) por el precio configurado. Las respuestas que no cumplen el esquema también se cobran, pero el proveedor no informa de sus tokens. No incluye los embeddings.");
        Line(report, "- **Recall total:** textos trampa con aviso del cambio / textos trampa verificados. Incluye los avisos que añade solo la comprobación de cifras por código.");
        Line(report, "- **Recall del verificador LLM:** textos trampa en los que el modelo verificador listó el dato cambiado. Es lo que aporta el modelo además del código. Las trampas en las que el verificador no respondió no cuentan.");
        Line(report);
        Line(report, "Ninguna de estas métricas demuestra que un texto sea de lectura fácil: la validación con personas lectoras sigue siendo necesaria. La similitud tampoco mide si se conservan los datos (\"15 días\" y \"16 días\" son casi iguales para un embedding).");
        Line(report);
        Line(report, "**Criterio eliminatorio:** un modelo que cambie o pierda un dato clave **sin aviso** queda descartado. La herramienta no puede saber qué no se ha avisado: revise a mano una muestra de los datos marcados como conservados, y tenga en cuenta el recall del verificador.");
        Line(report);
    }

    private static void Traps(StringBuilder report, IReadOnlyList<ModelEvaluation> evaluations)
    {
        Line(report, "## Textos trampa");
        Line(report);
        Line(report, $"En la adaptación profesional se cambia la primera cifra entera escrita con dígitos por la siguiente (+{TrapGenerator.Increment}). La verificación debería avisar del cambio. Como grupo de control se verifica también la adaptación sin alterar: un aviso con la cifra original que ya estaba allí no cuenta.");
        Line(report);
        Line(report, "| Modelo | Texto | Cambio | ¿Avisado? |");
        Line(report, "|---|---|---|---|");
        foreach (var evaluation in evaluations)
        {
            foreach (var trap in evaluation.Traps)
            {
                Line(report, $"| {Cell(evaluation.Profile.Name)} | {Cell(trap.TextId)} | {trap.OriginalFigure.ToString(Spanish)} → {trap.AlteredFigure.ToString(Spanish)} | {OutcomeLabel(trap.Outcome)} |");
            }
        }

        Line(report);
    }

    private static void Details(StringBuilder report, IReadOnlyList<ModelEvaluation> evaluations)
    {
        Line(report, "## Detalle por texto");
        Line(report);
        Line(report, "| Modelo | Texto | Resultado | Pautas | INFLESZ original | INFLESZ adaptado | Datos conservados | A revisar | Omitidos | Similitud | Tiempo | Tokens (entrada / salida) |");
        Line(report, "|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var evaluation in evaluations)
        {
            foreach (var text in evaluation.Texts)
            {
                Line(report, $"| {Cell(evaluation.Profile.Name)} | {Cell(text.TextId)} | {(text.IsSuccess ? "ok" : Cell(text.ErrorCode))} | {Percent(text.GuidelineCompliance)} | {Number(text.InfleszOriginal, "0.0")} | {Number(text.InfleszAdapted, "0.0")} | {Percent(text.KeyFactRetention)} | {text.KeyFactsToReview} | {text.KeyFactsOmitted} | {Number(text.Similarity, "0.000")} | {Seconds(text.Duration)} | {text.InputTokens} / {text.OutputTokens} |");
            }
        }

        Line(report);
    }

    private static void Configuration(StringBuilder report, RunInfo run, IReadOnlyList<ModelEvaluation> evaluations)
    {
        Line(report, "## Configuración (para repetir la evaluación)");
        Line(report);
        Line(report, $"- **Modo:** {(run.DryRun ? "simulación (`--dry-run`)" : "modelos reales")}");
        Line(report, $"- **Embeddings:** {Cell(run.EmbeddingModel)}");
        Line(report, $"- **Parámetros:** MaxOutputTokens = {run.Limits.MaxOutputTokens}, MaxAdaptationRetries = {run.Limits.MaxAdaptationRetries}, MaxWordsPerSentence = {run.Limits.MaxWordsPerSentence}, MaxWordsPerHeading = {run.Limits.MaxWordsPerHeading}, MaxCharactersPerAdaptationChunk = {run.Limits.MaxCharactersPerAdaptationChunk}, LlmTimeoutSeconds = {run.Limits.LlmTimeoutSeconds}");
        Line(report);
        Line(report, "| Prompt | SHA-256 |");
        Line(report, "|---|---|");
        foreach (var prompt in run.Prompts)
        {
            Line(report, $"| {Cell(prompt.Name)} | `{prompt.Sha256}` |");
        }

        Line(report);
        Line(report, $"| Modelo | Tipo | Proveedor | Adaptador | Verificador | Temperatura | Precio por millón (entrada / salida, {Cell(run.Currency)}) | Modelo que respondió | Versión del prompt |");
        Line(report, "|---|---|---|---|---|---|---|---|---|");
        foreach (var evaluation in evaluations)
        {
            var provider = evaluation.Profile.Provider;
            var pricing = evaluation.Profile.Pricing;
            var reported = string.Join(", ", evaluation.Texts.Select(text => text.ReportedModel).OfType<string>().Distinct(StringComparer.Ordinal));
            var prompts = string.Join(", ", evaluation.Texts.Select(text => text.PromptVersion).OfType<string>().Distinct(StringComparer.Ordinal));
            Line(report, $"| {Cell(evaluation.Profile.Name)} | {provider.Type} | {Cell(provider.DisplayName)} | {Cell(provider.AdapterModel)} | {Cell(provider.VerifierModel)} | {provider.Temperature.ToString(Spanish)} | {Money(pricing.InputPerMillion)} / {Money(pricing.OutputPerMillion)} | {Cell(reported)} | {Cell(prompts)} |");
        }

        Line(report);
        Line(report, "| Texto | Fuente | Licencia |");
        Line(report, "|---|---|---|");
        foreach (var text in run.Texts)
        {
            Line(report, $"| {Cell(text.Id)} | {Cell(text.Source)} | {Cell(text.License)} |");
        }
    }

    private static string OutcomeLabel(TrapOutcome outcome) => outcome switch
    {
        TrapOutcome.DetectedByVerifier => "Sí (verificador)",
        TrapOutcome.DetectedByCode => "Sí (solo código)",
        TrapOutcome.NotVerified => "Sin verificar",
        _ => "**No**",
    };

    private static string NotVerifiedNote(int count) =>
        count == 0 ? string.Empty : $"; {count} sin verificar";

    private static string Cell(string? text) => MarkdownCell.Escape(text);

    private static string Percent(double? value) => value is { } number ? number.ToString("0.0 %", Spanish) : NotMeasured;

    private static string Number(double? value, string format) => value is { } number ? number.ToString(format, Spanish) : NotMeasured;

    private static string Seconds(TimeSpan? value) => value is { } time ? $"{time.TotalSeconds.ToString("0.0", Spanish)} s" : NotMeasured;

    private static string Money(decimal value) => value.ToString("0.0000", Spanish);

    private static void Line(StringBuilder report, string text = "") => report.Append(text).Append('\n');
}
