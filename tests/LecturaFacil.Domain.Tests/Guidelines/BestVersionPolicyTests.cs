// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Domain.Tests.Guidelines;

public sealed class BestVersionPolicyTests
{
    private const string Text = "Texto de prueba suficientemente largo para los hallazgos de la prueba.";

    [Fact]
    public void PrefersRetry_WhenTheRetryHasFewerHighSeverityFindings_ReturnsTrue() =>
        BestVersionPolicy.PrefersRetry(Report(high: 2, low: 0), Report(high: 1, low: 5)).ShouldBeTrue();

    [Fact]
    public void PrefersRetry_WhenTheRetryHasMoreHighSeverityFindings_ReturnsFalse() =>
        BestVersionPolicy.PrefersRetry(Report(high: 1, low: 5), Report(high: 2, low: 0)).ShouldBeFalse();

    [Fact]
    public void PrefersRetry_WhenBothHaveTheSameHighSeverityFindings_PrefersFewerFindingsInTotal()
    {
        BestVersionPolicy.PrefersRetry(Report(high: 1, low: 3), Report(high: 1, low: 1)).ShouldBeTrue();
        BestVersionPolicy.PrefersRetry(Report(high: 1, low: 1), Report(high: 1, low: 3)).ShouldBeFalse();
    }

    [Fact]
    public void PrefersRetry_WhenBothAreEquallyGood_KeepsTheFirstVersion() =>
        BestVersionPolicy.PrefersRetry(Report(high: 1, low: 1), Report(high: 1, low: 1)).ShouldBeFalse();

    [Fact]
    public void PrefersRetry_WhenTheRetryCouldNotCheckASevereGuidelineTheFirstOneChecked_KeepsTheFirstVersion()
    {
        var acronyms = GuidelineCatalog.Find(GuidelineCodes.Acronyms);
        var current = GuidelineReport.Create(
            new GuidelineAnalysis([Finding.At(acronyms, Text, 0, 3, "Explica la sigla.")], []), [], maxStoredFindings: 10);
        var retry = GuidelineReport.Create(new GuidelineAnalysis([], [GuidelineCodes.Acronyms]), [], maxStoredFindings: 10);

        BestVersionPolicy.PrefersRetry(current, retry).ShouldBeFalse();
    }

    [Fact]
    public void PrefersRetry_WhenTheRetryLosesAFigureOfTheFirstVersion_KeepsTheFirstVersion()
    {
        var current = Content("El anuncio sale en el BOP.", "Puede pedirla hasta el 15 de diciembre de 2026.");
        var retry = Content("El anuncio sale en el boletín de la provincia.");

        BestVersionPolicy.PrefersRetry(current, Report(high: 1, low: 0), retry, Report(high: 0, low: 0)).ShouldBeFalse();
    }

    [Fact]
    public void PrefersRetry_WhenTheRetryKeepsEveryFigure_ComparesTheReports()
    {
        var current = Content("El anuncio sale en el BOP.", "Puede pedirla hasta el 15 de diciembre de 2026.");
        var retry = Content("El anuncio sale en el boletín.", "Tiene hasta el 15 de diciembre de 2026.");

        BestVersionPolicy.PrefersRetry(current, Report(high: 1, low: 0), retry, Report(high: 0, low: 0)).ShouldBeTrue();
    }

    [Fact]
    public void PrefersRetry_CountsTheFiguresOfTheGlossaryDefinitions()
    {
        var current = Content("Gana menos de 2,5 veces el IPREM.");
        var retry = AdaptedContent.TryCreate(
            "Ayudas",
            [new AdaptedSectionDraft("¿Quién la pide?", ["Gana menos de lo que dice el glosario."])],
            [new GlossaryTermDraft("Límite de ingresos", "2,5 veces el indicador público de renta.")])!;

        BestVersionPolicy.PrefersRetry(current, Report(high: 1, low: 0), retry, Report(high: 0, low: 0)).ShouldBeTrue();
    }

    private static AdaptedContent Content(params string[] sentences) =>
        AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Qué es?", sentences)], [])!;

    private static GuidelineReport Report(int high, int low)
    {
        var doubleNegation = GuidelineCatalog.Find(GuidelineCodes.DoubleNegation);
        var numericDate = GuidelineCatalog.Find(GuidelineCodes.NumericDate);
        var findings = Enumerable.Range(0, high).Select(index => Finding.At(doubleNegation, Text, index, index + 1, "Dilo en positivo."))
            .Concat(Enumerable.Range(0, low).Select(index => Finding.At(numericDate, Text, index, index + 1, "Escribe la fecha completa.")))
            .ToList();
        return GuidelineReport.Create(new GuidelineAnalysis(findings, []), [], maxStoredFindings: 100);
    }
}
