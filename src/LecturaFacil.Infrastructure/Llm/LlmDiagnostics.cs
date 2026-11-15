// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Llm;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Llamada mínima a cada proveedor configurado y a cada rol, para comprobar la configuración en desarrollo.
/// No envía ningún documento.
/// </summary>
public sealed class LlmDiagnostics(LlmProviders providers, IPromptLoader prompts)
{
    public const string PingPrompt = "ping.v1";

    private const string PingMessage = "ping";
    private const int PingMaxOutputTokens = 64;

    public async Task<IReadOnlyList<LlmPingResult>> PingAllAsync(CancellationToken cancellationToken)
    {
        var systemPrompt = prompts.Load(PingPrompt).Text;
        var results = new List<LlmPingResult>();
        foreach (var provider in providers.All)
        {
            foreach (var role in Enum.GetValues<LlmRole>())
            {
                var request = new LlmRequest(role, systemPrompt, PingMessage, PingMaxOutputTokens);
                var result = await provider.CompleteStructuredAsync<PingReply>(request, cancellationToken);
                results.Add(result.IsSuccess
                    ? LlmPingResult.Succeeded(role, result.Value)
                    : LlmPingResult.Failed(provider.Name, role, result.Error!.Code));
            }
        }

        return results;
    }
}

/// <summary>Respuesta esperada al ping.</summary>
public sealed record PingReply(bool Ok);

/// <summary>Resultado del ping a un proveedor y rol. Solo para desarrollo: incluye el modelo.</summary>
public sealed record LlmPingResult(
    string Provider,
    LlmRole Role,
    bool Success,
    string? Model,
    long? InputTokens,
    long? OutputTokens,
    long? DurationMs,
    string? Error)
{
    public static LlmPingResult Succeeded(LlmRole role, LlmResponse<PingReply> response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new(
            response.Provider,
            role,
            response.Value.Ok,
            response.Model,
            response.InputTokens,
            response.OutputTokens,
            (long)response.Duration.TotalMilliseconds,
            null);
    }

    public static LlmPingResult Failed(string provider, LlmRole role, string error) =>
        new(provider, role, false, null, null, null, null, error);
}
