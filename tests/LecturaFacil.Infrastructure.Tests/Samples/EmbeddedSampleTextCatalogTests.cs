// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Infrastructure.Samples;

namespace LecturaFacil.Infrastructure.Tests.Samples;

public sealed class EmbeddedSampleTextCatalogTests
{
    private static readonly LimitsOptions DefaultLimits = new();

    [Fact]
    public void All_ReturnsSixSamplesWithDistinctIds()
    {
        var samples = new EmbeddedSampleTextCatalog().All();

        samples.Count.ShouldBe(6);
        samples.Select(sample => sample.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(6);
    }

    [Fact]
    public void All_EverySampleHasATitleASourceAndATextThatCanBeSubmitted()
    {
        foreach (var sample in new EmbeddedSampleTextCatalog().All())
        {
            sample.Title.ShouldNotBeNullOrWhiteSpace();
            sample.Source.ShouldNotBeNullOrWhiteSpace();
            sample.Text.Length.ShouldBeInRange(DefaultLimits.MinCharacters, DefaultLimits.MaxCharacters);
        }
    }

    [Theory]
    [InlineData("@")]
    [InlineData("http")]
    [InlineData("www.")]
    public void All_NoSampleContainsEmailsOrLinks(string forbidden)
    {
        foreach (var sample in new EmbeddedSampleTextCatalog().All())
        {
            sample.Text.ShouldNotContain(forbidden, Case.Insensitive);
        }
    }

    [Fact]
    public void All_NoSampleContainsAnIdentityDocumentNumber()
    {
        foreach (var sample in new EmbeddedSampleTextCatalog().All())
        {
            System.Text.RegularExpressions.Regex.IsMatch(sample.Text, @"\b\d{8}[A-Za-z]\b").ShouldBeFalse();
        }
    }
}
