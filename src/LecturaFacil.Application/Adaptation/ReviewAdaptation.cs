// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Adaptation;

/// <summary>
/// Revisa las pautas de una adaptación con las mismas reglas deterministas que el texto original (US-05), sobre su
/// <see cref="AdaptedContent.PlainText"/>, más la de títulos claros. Las siglas y palabras difíciles del glosario
/// cuentan como explicadas.
/// </summary>
public sealed class ReviewAdaptation(IGuidelineChecker guidelineChecker, IOptions<LimitsOptions> limits)
{
    private readonly LimitsOptions _limits = limits.Value;

    public GuidelineReport Execute(AdaptedContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var analysis = guidelineChecker.Check(content.PlainText);
        return GuidelineReport.ForAdaptation(content, analysis, _limits.MaxWordsPerHeading, _limits.MaxFindingsPerDocument);
    }
}
