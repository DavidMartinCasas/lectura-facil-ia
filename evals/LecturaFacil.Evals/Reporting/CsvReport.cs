// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Text;

namespace LecturaFacil.Evals.Reporting;

/// <summary>
/// Datos en bruto: una fila por texto y modelo (<c>kind = text</c>) y otra por texto trampa y modelo
/// (<c>kind = trap</c>). Números con punto decimal; un valor que no se ha podido medir queda vacío.
/// </summary>
public static class CsvReport
{
    public const string Header =
        "model,kind,text_id,result,guideline_compliance,inflesz_original,inflesz_adapted,inflesz_delta,key_fact_retention,key_fact_warnings,similarity,duration_ms,input_tokens,output_tokens,llm_duration_ms,cost,trap_original_figure,trap_altered_figure,trap_detected";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Render(IReadOnlyList<ModelEvaluation> evaluations)
    {
        ArgumentNullException.ThrowIfNull(evaluations);

        var csv = new StringBuilder().Append(Header).Append('\n');
        foreach (var evaluation in evaluations)
        {
            var model = evaluation.Profile.Name;
            foreach (var text in evaluation.Texts)
            {
                Row(csv, Text(model), "text", Text(text.TextId), Text(text.ErrorCode ?? "ok"), Number(text.GuidelineCompliance),
                    Number(text.InfleszOriginal), Number(text.InfleszAdapted), Number(text.InfleszDelta), Number(text.KeyFactRetention),
                    Number(text.KeyFactWarnings), Number(text.Similarity), Number((long)text.Duration.TotalMilliseconds),
                    Number(text.InputTokens), Number(text.OutputTokens), Number((long)text.LlmDuration.TotalMilliseconds),
                    Number(text.Cost), string.Empty, string.Empty, string.Empty);
            }

            foreach (var trap in evaluation.Traps)
            {
                Row(csv, Text(model), "trap", Text(trap.TextId), Text("ok"), string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, string.Empty, Number(trap.InputTokens), Number(trap.OutputTokens),
                    string.Empty, Number(trap.Cost), Number(trap.OriginalFigure), Number(trap.AlteredFigure), trap.Detected ? "true" : "false");
            }
        }

        return csv.ToString();
    }

    private static void Row(StringBuilder csv, params string[] fields) => csv.AppendJoin(',', fields).Append('\n');

    /// <summary>
    /// Entre comillas, con las comillas dobladas. Un texto que empieza por <c>= + - @</c> o un tabulador se precede de
    /// un apóstrofo para que una hoja de cálculo no lo ejecute como fórmula.
    /// </summary>
    private static string Text(string value)
    {
        var safe = value.Length > 0 && "=+-@\t\r".Contains(value[0], StringComparison.Ordinal) ? $"'{value}" : value;
        return $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string Number(double? value) => value?.ToString("0.######", Invariant) ?? string.Empty;

    private static string Number(decimal value) => value.ToString(Invariant);

    private static string Number(long value) => value.ToString(Invariant);
}
