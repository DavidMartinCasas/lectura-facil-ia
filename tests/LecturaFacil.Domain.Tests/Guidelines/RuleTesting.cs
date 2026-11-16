// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Domain.Tests.Guidelines;

internal static class RuleTesting
{
    public static List<Finding> Check(this IGuidelineRule rule, string text) => [.. rule.Check(GuidelineText.Create(text))];

    /// <summary>El fragmento de cada hallazgo coincide con el texto entre su inicio y su fin.</summary>
    public static void ShouldPointToFragments(this IEnumerable<Finding> findings, string text)
    {
        foreach (var finding in findings)
        {
            text[finding.Start..finding.End].ShouldBe(finding.Fragment);
        }
    }
}
