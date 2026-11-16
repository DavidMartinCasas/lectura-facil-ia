// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Domain.Tests.Guidelines;

public sealed class FindingTests
{
    private static readonly Guideline Acronyms = GuidelineCatalog.Find(GuidelineCodes.Acronyms);

    [Fact]
    public void At_WhenSpanIsValid_TakesFragmentAndSeverityFromTextAndGuideline()
    {
        var finding = Finding.At(Acronyms, "Publicado en el BOP.", 16, 19, "Explica la sigla.");

        finding.GuidelineCode.ShouldBe(GuidelineCodes.Acronyms);
        finding.Fragment.ShouldBe("BOP");
        finding.Start.ShouldBe(16);
        finding.End.ShouldBe(19);
        finding.Severity.ShouldBe(Acronyms.DefaultSeverity);
        finding.Suggestion.ShouldBe("Explica la sigla.");
    }

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(3, 3)]
    [InlineData(4, 3)]
    [InlineData(0, 21)]
    public void At_WhenSpanIsOutsideTheText_Throws(int start, int end)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Finding.At(Acronyms, "Publicado en el BOP.", start, end, "x"));
    }

    [Fact]
    public void At_WhenSuggestionIsEmpty_Throws()
    {
        Should.Throw<ArgumentException>(() => Finding.At(Acronyms, "Publicado en el BOP.", 16, 19, " "));
    }

    [Fact]
    public void Catalog_HasOneGuidelinePerCodeWithDescription()
    {
        GuidelineCatalog.All.Select(guideline => guideline.Code).ShouldBeUnique();
        GuidelineCatalog.All.ShouldAllBe(guideline => !string.IsNullOrWhiteSpace(guideline.Description));
        GuidelineCatalog.All.Count.ShouldBe(12);
    }

    [Fact]
    public void Catalog_WhenCodeIsUnknown_Throws()
    {
        Should.Throw<KeyNotFoundException>(() => GuidelineCatalog.Find("NO_EXISTE"));
    }
}
