// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;
using LecturaFacil.Infrastructure.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace LecturaFacil.Infrastructure.Tests.Llm;

public sealed class AzureFoundryProviderTests
{
    [Theory]
    [InlineData("https://mi-recurso.openai.azure.com")]
    [InlineData("https://mi-recurso.openai.azure.com/")]
    [InlineData("https://mi-recurso.openai.azure.com/openai/v1")]
    [InlineData("https://mi-recurso.openai.azure.com/openai/v1/")]
    public void V1Endpoint_FromTheResourceOrTheV1Url_PointsToTheV1Api(string endpoint)
    {
        AzureFoundryProvider.V1Endpoint(endpoint).ShouldBe(new Uri("https://mi-recurso.openai.azure.com/openai/v1"));
    }

    [Fact]
    public async Task Complete_CallsTheV1ApiWithoutApiVersionAndTheKeyOnlyInAHeader()
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""");

        await CompleteAsync(handler, LlmRole.Adapter, "https://mi-recurso.services.ai.azure.com/");

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Uri.ShouldBe(new Uri("https://mi-recurso.services.ai.azure.com/openai/v1/chat/completions"));
        request.Uri.Query.ShouldBeEmpty();
        request.Headers["Authorization"].ShouldBe($"Bearer {LlmTestOptions.ApiKey}");
        request.Body.ShouldNotContain(LlmTestOptions.ApiKey);
    }

    [Theory]
    [InlineData(LlmRole.Adapter, "despliegue-adaptador")]
    [InlineData(LlmRole.Verifier, "despliegue-verificador")]
    public async Task Complete_SendsTheDeploymentNameAsTheModel(LlmRole role, string deployment)
    {
        var handler = FakeHttpMessageHandler.ReturningCompletion("""{"ok":true}""", model: "gpt-4.1-mini-2025-04-14");

        var result = await CompleteAsync(handler, role, "https://mi-recurso.openai.azure.com/");

        handler.Requests.ShouldHaveSingleItem().Json.GetProperty("model").GetString().ShouldBe(deployment);
        result.Value.Model.ShouldBe(deployment);
        result.Value.Provider.ShouldBe("Nube de pruebas");
    }

    private static async Task<Result<LlmResponse<PingReply>>> CompleteAsync(FakeHttpMessageHandler handler, LlmRole role, string endpoint)
    {
        var options = LlmTestOptions.Provider(LlmProviderType.AzureFoundry, endpoint);
        using var httpClient = new HttpClient(handler);
        var provider = new AzureFoundryProvider(options, httpClient, new LimitsOptions(), NullLogger.Instance);
        return await provider.CompleteStructuredAsync<PingReply>(
            new LlmRequest(role, "sistema", "usuario", 100),
            TestContext.Current.CancellationToken);
    }
}
