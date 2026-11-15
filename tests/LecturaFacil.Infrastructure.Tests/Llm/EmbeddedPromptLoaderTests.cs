// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Llm;

namespace LecturaFacil.Infrastructure.Tests.Llm;

public sealed class EmbeddedPromptLoaderTests
{
    private readonly EmbeddedPromptLoader _loader = new();

    [Fact]
    public void Load_WhenThePromptIsEmbedded_ReturnsItsNameAndText()
    {
        var prompt = _loader.Load(LlmDiagnostics.PingPrompt);

        prompt.Name.ShouldBe("ping.v1");
        prompt.Text.ShouldContain("JSON");
    }

    [Fact]
    public void Load_TheSamePromptTwice_ReturnsTheSameInstance()
    {
        _loader.Load(LlmDiagnostics.PingPrompt).ShouldBeSameAs(_loader.Load(LlmDiagnostics.PingPrompt));
    }

    [Fact]
    public void Load_WhenThePromptDoesNotExist_Throws()
    {
        Should.Throw<InvalidOperationException>(() => _loader.Load("no-existe.v1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ping")]
    [InlineData("../ping.v1")]
    [InlineData("Ping.V1")]
    public void Load_WhenTheNameIsNotAVersionedPromptName_Throws(string name)
    {
        Should.Throw<ArgumentException>(() => _loader.Load(name));
    }
}
