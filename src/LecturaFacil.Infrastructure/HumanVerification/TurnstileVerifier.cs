// SPDX-License-Identifier: Apache-2.0
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.HumanVerification;

/// <summary>
/// Valida en el servidor el token del widget de Cloudflare Turnstile con la API <c>siteverify</c>. Cualquier fallo
/// (token rechazado, respuesta inesperada, error de red o tiempo agotado) rechaza la petición: fallo seguro.
/// </summary>
public sealed partial class TurnstileVerifier(
    HttpClient httpClient,
    IOptions<TurnstileOptions> options,
    ILogger<TurnstileVerifier> logger) : IHumanVerification
{
    public static readonly Uri SiteVerifyUri = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");

    /// <summary>Longitud máxima de un token según la documentación de Turnstile.</summary>
    public const int MaxTokenLength = 2048;

    public bool IsEnabled => true;

    public async Task<Result> VerifyAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
        {
            return Result.Failure(HumanVerificationErrors.Failed);
        }

        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = options.Value.SecretKey,
                ["response"] = token,
            });
            using var response = await httpClient.PostAsync(SiteVerifyUri, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Unavailable(logger, (int)response.StatusCode);
                return Result.Failure(HumanVerificationErrors.Failed);
            }

            var reply = await response.Content.ReadFromJsonAsync<SiteVerifyReply>(cancellationToken);
            return reply?.Success == true ? Result.Success() : Result.Failure(HumanVerificationErrors.Failed);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Nunca el token ni la clave: solo el tipo de fallo.
            Unreachable(logger, exception.GetType().Name);
            return Result.Failure(HumanVerificationErrors.Failed);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Turnstile ha respondido {StatusCode}; la petición se rechaza.")]
    private static partial void Unavailable(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se ha podido validar el token de Turnstile ({ErrorType}); la petición se rechaza.")]
    private static partial void Unreachable(ILogger logger, string errorType);

    private sealed record SiteVerifyReply([property: JsonPropertyName("success")] bool Success);
}
