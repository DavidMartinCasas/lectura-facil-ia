// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Adaptation;

namespace LecturaFacil.Domain.Tests.Adaptation;

public sealed class TextChunkerTests
{
    [Fact]
    public void Split_WhenTextFitsTheLimit_ReturnsASingleChunk()
    {
        const string text = "Primer párrafo.\n\nSegundo párrafo.";

        TextChunker.Split(text, 100).ShouldBe([text]);
    }

    [Fact]
    public void Split_WhenTextIsLonger_KeepsWholeParagraphsTogether()
    {
        var first = new string('a', 40);
        var second = new string('b', 40);
        var third = new string('c', 40);

        var chunks = TextChunker.Split($"{first}\n\n{second}\n\n{third}", 90);

        chunks.ShouldBe([$"{first}\n\n{second}", third]);
    }

    [Fact]
    public void Split_WhenAParagraphIsTooLong_SplitsItBySentences()
    {
        const string paragraph = "Esta es la primera frase del párrafo. Esta es la segunda frase del párrafo. Y esta es la tercera.";

        var chunks = TextChunker.Split(paragraph, 80);

        chunks.ShouldBe(["Esta es la primera frase del párrafo. Esta es la segunda frase del párrafo.", "Y esta es la tercera."]);
    }

    [Fact]
    public void Split_WhenASentenceIsTooLong_SplitsItBetweenWords()
    {
        var sentence = string.Join(' ', Enumerable.Repeat("palabra", 20));

        var chunks = TextChunker.Split(sentence, 30);

        chunks.ShouldAllBe(chunk => chunk.Length <= 30);
        string.Join(' ', chunks).ShouldBe(sentence);
    }

    [Fact]
    public void Split_WhenAWordIsLongerThanTheLimit_CutsItWithoutBreakingSurrogatePairs()
    {
        var word = string.Concat(Enumerable.Repeat("a😀", 20));

        var chunks = TextChunker.Split(word, 15);

        chunks.ShouldAllBe(chunk => chunk.Length <= 15 && !char.IsHighSurrogate(chunk[chunk.Length - 1]));
        string.Concat(chunks).ShouldBe(word);
    }

    [Fact]
    public void Split_NeverReturnsEmptyChunks()
    {
        var chunks = TextChunker.Split("  Uno.\n\n\n\n   \n\nDos.  ", 6);

        chunks.ShouldBe(["Uno.", "Dos."]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Split_WhenTheLimitIsNotPositive_Throws(int maxCharacters) =>
        Should.Throw<ArgumentOutOfRangeException>(() => TextChunker.Split("Texto.", maxCharacters));
}
