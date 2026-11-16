// SPDX-License-Identifier: Apache-2.0
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
