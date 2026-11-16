// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Evals.Metrics;
using LecturaFacil.Evals.Similarity;

namespace LecturaFacil.Evals.Tests.Similarity;

public sealed class LexicalEmbeddingProviderTests
{
    [Fact]
    public void Embed_WhenTextsHaveTheSameWords_ReturnsTheSameVector()
    {
        var first = LexicalEmbeddingProvider.Embed("Tiene 15 días para pedirla.");
        var second = LexicalEmbeddingProvider.Embed("tiene 15 DÍAS, para pedirla");

        EvalMetrics.CosineSimilarity(first, second).ShouldBe(1, tolerance: 1e-9);
    }

    [Fact]
    public void Embed_WhenTextsShareNoWords_ReturnsLowerSimilarity()
    {
        var first = LexicalEmbeddingProvider.Embed("Tiene 15 días para pedirla.");
        var related = LexicalEmbeddingProvider.Embed("Tiene 10 días para pedirla.");
        var unrelated = LexicalEmbeddingProvider.Embed("Ayuntamiento alquiler vivienda.");

        EvalMetrics.CosineSimilarity(first, related).ShouldBeGreaterThan(EvalMetrics.CosineSimilarity(first, unrelated));
    }

    [Fact]
    public void Embed_Always_ReturnsAVectorOfFixedSize()
    {
        LexicalEmbeddingProvider.Embed(string.Empty).Count.ShouldBe(LexicalEmbeddingProvider.Dimensions);
    }

    [Fact]
    public async Task EmbedAsync_WhenCalled_ReturnsTheSameAsEmbed()
    {
        var provider = new LexicalEmbeddingProvider();

        var vector = await provider.EmbedAsync("Hola", TestContext.Current.CancellationToken);

        vector.ShouldBe(LexicalEmbeddingProvider.Embed("Hola"));
    }
}

public sealed class OpenAIEmbeddingProviderTests
{
    [Theory]
    [InlineData(LecturaFacil.Infrastructure.Llm.LlmProviderType.OpenAICompatible, "http://localhost:8080/v1")]
    [InlineData(LecturaFacil.Infrastructure.Llm.LlmProviderType.AzureFoundry, "https://recurso.invalid/")]
    public void Constructor_WhenConfigured_ExposesTheModelWithoutCallingIt(LecturaFacil.Infrastructure.Llm.LlmProviderType type, string endpoint)
    {
        using var provider = new OpenAIEmbeddingProvider(
            new LecturaFacil.Evals.Configuration.EmbeddingSettings(type, endpoint, "clave-de-prueba", "bge-m3"),
            TimeSpan.FromSeconds(5));

        provider.Model.ShouldBe("bge-m3");
    }
}
