// SPDX-License-Identifier: Apache-2.0
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json.Nodes;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Añade <c>"chat_template_kwargs": {"enable_thinking": false}</c> a cada petición de chat para que los modelos
/// híbridos (Qwen3, Gemma 4…) no razonen antes de responder (<see cref="LlmProviderOptions.DisableThinking"/>).
/// El SDK de OpenAI no tiene una opción estable para campos extra, por eso se hace en el pipeline HTTP.
/// </summary>
internal sealed class DisableThinkingPolicy : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        AddChatTemplateArguments(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        AddChatTemplateArguments(message);
        await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
    }

    private static void AddChatTemplateArguments(PipelineMessage message)
    {
        if (message.Request.Content is not { } content)
        {
            return;
        }

        using var buffer = new MemoryStream();
        content.WriteTo(buffer);
        if (JsonNode.Parse(buffer.ToArray()) is not JsonObject body)
        {
            return;
        }

        var arguments = body["chat_template_kwargs"] as JsonObject ?? [];
        arguments["enable_thinking"] = false;
        body["chat_template_kwargs"] = arguments;
        message.Request.Content = BinaryContent.Create(BinaryData.FromString(body.ToJsonString()));
    }
}
