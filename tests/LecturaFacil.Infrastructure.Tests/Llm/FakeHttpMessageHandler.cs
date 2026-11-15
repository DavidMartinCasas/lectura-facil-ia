// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text;
using System.Text.Json;

namespace LecturaFacil.Infrastructure.Tests.Llm;

/// <summary>Sustituye a la red: guarda cada petición y devuelve la respuesta indicada.</summary>
internal sealed class FakeHttpMessageHandler(Func<CapturedRequest, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    private readonly List<CapturedRequest> _requests = [];

    public IReadOnlyList<CapturedRequest> Requests => _requests;

    public static FakeHttpMessageHandler Returning(HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));

    /// <summary>Respuesta de chat completions con el contenido indicado.</summary>
    public static FakeHttpMessageHandler ReturningCompletion(string content, string model = "modelo-respuesta") =>
        Returning(HttpStatusCode.OK, ChatCompletion(content, model));

    public static string ChatCompletion(string content, string model) => JsonSerializer.Serialize(new
    {
        id = "chatcmpl-prueba",
        @object = "chat.completion",
        created = 1_700_000_000,
        model,
        choices = new[]
        {
            new { index = 0, message = new { role = "assistant", content }, finish_reason = "stop" },
        },
        usage = new { prompt_tokens = 120, completion_tokens = 7, total_tokens = 127 },
    });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var captured = new CapturedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase),
            body);
        _requests.Add(captured);
        return await respond(captured, cancellationToken);
    }
}

internal sealed record CapturedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string Body)
{
    public JsonElement Json => JsonDocument.Parse(Body).RootElement;
}
