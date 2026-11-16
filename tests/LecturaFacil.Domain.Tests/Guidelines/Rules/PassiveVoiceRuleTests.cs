// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Guidelines.Rules;

namespace LecturaFacil.Domain.Tests.Guidelines.Rules;

public sealed class PassiveVoiceRuleTests
{
    private readonly PassiveVoiceRule _rule = new();

    [Theory]
    [InlineData("La solicitud será revisada por el Ayuntamiento.", "será revisada")]
    [InlineData("Las ayudas fueron concedidas en marzo.", "fueron concedidas")]
    [InlineData("La ayuda ha sido aprobada.", "sido aprobada")]
    [InlineData("El plazo está previsto en la ley.", "está previsto")]
    [InlineData("La lista es publicada rápidamente.", "es publicada")]
    [InlineData("La lista será finalmente publicada.", "será finalmente publicada")]
    [InlineData("La solicitud se publicará en el boletín.", "se publicará")]
    [InlineData("Las listas se publican en la web.", "se publican")]
    [InlineData("Las solicitudes deberán presentarse en el registro.", "deberán presentarse")]
    public void Check_WhenSentenceIsPassive_ReturnsIt(string text, string fragment)
    {
        var findings = _rule.Check(text);

        var finding = findings.ShouldHaveSingleItem();
        finding.GuidelineCode.ShouldBe(GuidelineCodes.PassiveVoice);
        finding.Fragment.ShouldBe(fragment);
        findings.ShouldPointToFragments(text);
    }

    [Theory]
    [InlineData("El Ayuntamiento revisará la solicitud.")]
    [InlineData("El Ayuntamiento da ayudas. Puede pedirlas en la web.")]
    [InlineData("Es nada. Es cada día.")]
    [InlineData("Ser joven es bueno.")]
    public void Check_WhenSentenceIsActive_ReturnsNothing(string text)
    {
        _rule.Check(text).ShouldBeEmpty();
    }
}
