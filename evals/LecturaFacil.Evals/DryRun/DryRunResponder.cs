// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Llm;
using LecturaFacil.Evals.Metrics;
using LecturaFacil.Testing;

namespace LecturaFacil.Evals.DryRun;

/// <summary>
/// Respuestas de <see cref="FakeLlmProvider"/> para <c>--dry-run</c>. El adaptador simulado devuelve el propio texto,
/// una frase por línea, con el esquema del adaptador; el verificador simulado no lista ningún dato, así que solo
/// actúa la comprobación de cifras por código. Nunca sale a la red. Los tokens son una estimación (4 caracteres por token).
/// </summary>
public static class DryRunResponder
{
    public const string ProviderName = "Simulación (dry-run)";
    public const string AdapterModel = "adaptador-simulado";
    public const string VerifierModel = "verificador-simulado";

    private const string EmptyVerification = """{"datos_clave": []}""";
    private static readonly char[] SentenceEnds = ['.', '\n', '?', '!', ';'];

    public static LlmRecording Respond(LlmRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var inputTokens = (request.SystemPrompt.Length + request.UserMessage.Length) / CostEstimator.CharactersPerToken;
        if (request.Role == LlmRole.Verifier)
        {
            return new LlmRecording(VerifierModel, EmptyVerification, inputTokens, EmptyVerification.Length / CostEstimator.CharactersPerToken, 0);
        }

        var content = JsonSerializer.Serialize(Adapt(DelimitedDocument(request.UserMessage)));
        return new LlmRecording(AdapterModel, content, inputTokens, content.Length / CostEstimator.CharactersPerToken, 0);
    }

    private static AdaptationReply Adapt(string document)
    {
        var sentences = document
            .Split(SentenceEnds, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(sentence => $"{sentence}.")
            .ToList();
        return new AdaptationReply(
            "Texto adaptado (simulación)",
            [new AdaptationReplySection("¿Qué dice el texto?", sentences)],
            []);
    }

    /// <summary>El texto que va entre los delimitadores de <see cref="LlmRequest.ForDocument"/>: sin la primera ni la última línea.</summary>
    private static string DelimitedDocument(string userMessage)
    {
        var start = userMessage.IndexOf('\n', StringComparison.Ordinal) + 1;
        var end = userMessage.LastIndexOf('\n');
        return end > start ? userMessage[start..end] : userMessage;
    }
}
