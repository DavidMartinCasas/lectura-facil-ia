// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Readability;

namespace LecturaFacil.Domain.Tests.Readability;

public sealed class SpanishSyllableCounterTests
{
    [Theory]
    [InlineData("ayuntamiento", 5)]
    [InlineData("día", 2)]
    [InlineData("construcción", 3)]
    [InlineData("guion", 1)]
    [InlineData("país", 2)]
    public void Count_WhenWordIsFromTheAcceptanceCriteria_ReturnsTheExpectedSyllables(string word, int expected)
    {
        SpanishSyllableCounter.Count(word).ShouldBe(expected);
    }

    [Theory]
    [InlineData("ciudad", 2)]
    [InlineData("hielo", 2)]
    [InlineData("fue", 1)]
    [InlineData("aire", 2)]
    [InlineData("causa", 2)]
    [InlineData("ruido", 2)]
    [InlineData("construí", 2)]
    [InlineData("estudiáis", 3)]
    public void Count_WhenWordHasADiphthong_CountsItAsOneSyllable(string word, int expected)
    {
        SpanishSyllableCounter.Count(word).ShouldBe(expected);
    }

    [Theory]
    [InlineData("río", 2)]
    [InlineData("baúl", 2)]
    [InlineData("reír", 2)]
    [InlineData("maíz", 2)]
    [InlineData("veía", 3)]
    [InlineData("oído", 3)]
    [InlineData("leer", 2)]
    [InlineData("poeta", 3)]
    [InlineData("aéreo", 4)]
    [InlineData("chiita", 3)]
    public void Count_WhenWordHasAHiatus_CountsEachVowelAsASyllable(string word, int expected)
    {
        SpanishSyllableCounter.Count(word).ShouldBe(expected);
    }

    [Theory]
    [InlineData("buey", 1)]
    [InlineData("Uruguay", 3)]
    [InlineData("averiguáis", 4)]
    [InlineData("miau", 1)]
    public void Count_WhenWordHasATriphthong_CountsItAsOneSyllable(string word, int expected)
    {
        SpanishSyllableCounter.Count(word).ShouldBe(expected);
    }

    [Theory]
    [InlineData("queso", 2)]
    [InlineData("quien", 1)]
    [InlineData("guerra", 2)]
    [InlineData("guía", 2)]
    [InlineData("aguante", 3)]
    [InlineData("pingüino", 3)]
    [InlineData("argüir", 2)]
    public void Count_WhenUIsSilentOrHasDiaeresis_CountsOnlyPronouncedVowels(string word, int expected)
    {
        SpanishSyllableCounter.Count(word).ShouldBe(expected);
    }

    [Theory]
    [InlineData("y", 1)]
    [InlineData("hoy", 1)]
    [InlineData("rey", 1)]
    [InlineData("muy", 1)]
    [InlineData("leyes", 2)]
    [InlineData("ayer", 2)]
    [InlineData("yo", 1)]
    public void Count_WhenWordHasY_TreatsItAsVowelOnlyWhenNoVowelFollows(string word, int expected)
    {
        SpanishSyllableCounter.Count(word).ShouldBe(expected);
    }

    [Theory]
    [InlineData("llave", 2)]
    [InlineData("perro", 2)]
    [InlineData("chico", 2)]
    [InlineData("problema", 3)]
    [InlineData("tabla", 2)]
    [InlineData("trabajo", 3)]
    [InlineData("transporte", 3)]
    [InlineData("instrucción", 3)]
    [InlineData("obstáculo", 4)]
    public void Count_WhenWordHasConsonantClusters_CountsOneSyllablePerVowelNucleus(string word, int expected)
    {
        SpanishSyllableCounter.Count(word).ShouldBe(expected);
    }

    [Theory]
    [InlineData("ahora", 3)]
    [InlineData("búho", 2)]
    [InlineData("rehén", 2)]
    [InlineData("huevo", 2)]
    public void Count_WhenWordHasH_TreatsItAsASyllableBoundary(string word, int expected)
    {
        SpanishSyllableCounter.Count(word).ShouldBe(expected);
    }

    [Theory]
    [InlineData("solicitud", 4)]
    [InlineData("administración", 5)]
    [InlineData("documentación", 5)]
    [InlineData("subvención", 3)]
    [InlineData("plazo", 2)]
    [InlineData("ciudadanía", 5)]
    public void Count_WhenWordIsAdministrative_ReturnsTheExpectedSyllables(string word, int expected)
    {
        SpanishSyllableCounter.Count(word).ShouldBe(expected);
    }

    [Theory]
    [InlineData("AYUNTAMIENTO", 5)]
    [InlineData("Resolución", 4)]
    [InlineData("PAÍS", 2)]
    public void Count_WhenWordHasUppercaseOrDecomposedAccents_ReturnsTheSameAsLowercase(string word, int expected)
    {
        SpanishSyllableCounter.Count(word).ShouldBe(expected);
    }

    [Theory]
    [InlineData("pvp")]
    [InlineData("Sr")]
    public void Count_WhenWordHasNoVowels_ReturnsOne(string word)
    {
        SpanishSyllableCounter.Count(word).ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    public void Count_WhenThereIsNoLetter_ReturnsZero(string word)
    {
        SpanishSyllableCounter.Count(word).ShouldBe(0);
    }
}
