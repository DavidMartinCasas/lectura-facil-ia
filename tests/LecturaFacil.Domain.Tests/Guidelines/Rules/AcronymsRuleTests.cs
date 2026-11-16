// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Guidelines.Rules;

namespace LecturaFacil.Domain.Tests.Guidelines.Rules;

public sealed class AcronymsRuleTests
{
    private readonly AcronymsRule _rule = new(["OK", "TV", "DNI"]);

    [Fact]
    public void Check_WhenAcronymIsNotExplained_ReturnsItWithItsStartAndEnd()
    {
        const string text = "La solicitud se publicará en el BOP.";

        var finding = _rule.Check(text).ShouldHaveSingleItem();

        finding.GuidelineCode.ShouldBe(GuidelineCodes.Acronyms);
        finding.Fragment.ShouldBe("BOP");
        finding.Start.ShouldBe(32);
        finding.End.ShouldBe(35);
        finding.Severity.ShouldBe(Severity.High);
        finding.Suggestion.ShouldContain("BOP");
    }

    [Fact]
    public void Check_WhenAcronymIsAllowed_ReturnsNothing()
    {
        _rule.Check("Todo está OK. Lo verás en la TV. Traiga su DNI.").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Se publica en el Boletín Oficial de la Provincia (BOP). Después, el BOP abre el plazo.")]
    [InlineData("Se publica en el BOP (Boletín Oficial de la Provincia). Después, el BOP abre el plazo.")]
    public void Check_WhenAcronymIsExplainedOnce_ReturnsNothing(string text)
    {
        _rule.Check(text).ShouldBeEmpty();
    }

    [Fact]
    public void Check_WhenAcronymAppearsTwice_ReturnsBothOccurrences()
    {
        const string text = "Ganar menos que el IPREM. El IPREM cambia cada año.";

        var findings = _rule.Check(text);

        findings.Count.ShouldBe(2);
        findings.ShouldPointToFragments(text);
    }

    [Theory]
    [InlineData("Lea el capítulo IV y el anexo XII.")]
    [InlineData("Una sola letra A no es sigla.")]
    [InlineData("Una palabra en MAYÚSCULAS larga no es sigla.")]
    [InlineData("El Ayuntamiento da ayudas.")]
    [InlineData("PLAZO DE SOLICITUD\nPuede pedirla en la web.")]
    [InlineData("BASES DE LA CONVOCATORIA\nEl plazo es corto.")]
    public void Check_WhenUppercaseWordIsNotAnAcronym_ReturnsNothing(string text)
    {
        _rule.Check(text).ShouldBeEmpty();
    }

    [Fact]
    public void Check_WhenHeadingHasAnAcronym_IgnoresItButNotInTheBody()
    {
        const string text = "AYUDAS DEL IPREM\nGana menos que el IPREM.";

        _rule.Check(text).ShouldHaveSingleItem().Start.ShouldBe(text.LastIndexOf("IPREM", StringComparison.Ordinal));
    }

    [Fact]
    public void Check_WhenAcronymHasAccents_ReturnsIt()
    {
        _rule.Check("Lo gestiona la ONCÉ.").ShouldHaveSingleItem().Fragment.ShouldBe("ONCÉ");
    }
}
