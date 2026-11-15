// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Domain.Tests.Documents;

public sealed class OriginalTextTests
{
    private static readonly TextLengthLimits Limits = new(10, 40);

    [Fact]
    public void Normalize_WhenTextHasWindowsLineBreaks_UsesOnlyLineFeeds()
    {
        OriginalText.Normalize("Uno\r\nDos\rTres").ShouldBe("Uno\nDos\nTres");
    }

    [Fact]
    public void Normalize_WhenTextHasControlCharacters_RemovesThemButKeepsLineBreaks()
    {
        OriginalText.Normalize("Uno\u0000 dos\u0007\n\ntres\u001B\u007F\u0085").ShouldBe("Uno dos\n\ntres");
    }

    [Fact]
    public void Normalize_WhenTextHasTabs_ReplacesThemWithSpaces()
    {
        OriginalText.Normalize("Uno\tdos").ShouldBe("Uno dos");
    }

    [Fact]
    public void Normalize_WhenTextIsDecomposed_NormalizesToNfc()
    {
        OriginalText.Normalize("Resolucio\u0301n").ShouldBe("Resolución");
    }

    [Fact]
    public void Normalize_WhenTextHasSurroundingWhitespace_TrimsIt()
    {
        OriginalText.Normalize("\n\n  Texto  \n").ShouldBe("Texto");
    }

    [Theory]
    [InlineData("corto", TextLengthCheck.TooShort)]
    [InlineData("exactamente10", TextLengthCheck.Valid)]
    [InlineData("1234567890", TextLengthCheck.Valid)]
    [InlineData("1234567890123456789012345678901234567890", TextLengthCheck.Valid)]
    [InlineData("12345678901234567890123456789012345678901", TextLengthCheck.TooLong)]
    public void CheckLength_WhenCalled_ComparesWithTheLimits(string text, TextLengthCheck expected)
    {
        OriginalText.CheckLength(text, Limits).ShouldBe(expected);
    }

    [Fact]
    public void CheckLength_WhenTextOnlyReachesTheMinimumBeforeNormalizing_IsTooShort()
    {
        OriginalText.CheckLength("  corto\u0000\u0000  ", Limits).ShouldBe(TextLengthCheck.TooShort);
    }

    [Fact]
    public void Create_WhenTextIsValid_ReturnsTheNormalizedText()
    {
        OriginalText.Create("  Texto\r\nde prueba ", Limits).Value.ShouldBe("Texto\nde prueba");
    }

    [Theory]
    [InlineData("corto")]
    [InlineData("12345678901234567890123456789012345678901")]
    public void Create_WhenLengthIsOutOfLimits_Throws(string text)
    {
        Should.Throw<ArgumentException>(() => OriginalText.Create(text, Limits));
    }

    [Fact]
    public void Restore_WhenTextIsEmpty_Throws()
    {
        Should.Throw<ArgumentException>(() => OriginalText.Restore(" "));
    }

    [Fact]
    public void Restore_WhenTextWasStored_KeepsItWithoutApplyingTheLimits()
    {
        OriginalText.Restore("corto").Value.ShouldBe("corto");
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 9)]
    public void TextLengthLimits_WhenRangeIsInvalid_Throws(int minimum, int maximum)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new TextLengthLimits(minimum, maximum));
    }
}
