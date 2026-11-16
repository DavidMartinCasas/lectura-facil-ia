// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Guidelines;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Guidelines;

/// <summary>Analizador de pautas con los umbrales de <see cref="LimitsOptions"/> y las listas embebidas. Sin estado: singleton.</summary>
public sealed partial class GuidelineChecker : IGuidelineChecker
{
    private readonly GuidelineAnalyzer _analyzer;
    private readonly ILogger<GuidelineChecker> _logger;

    public GuidelineChecker(IOptions<LimitsOptions> limits, ILogger<GuidelineChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(limits);

        var settings = new GuidelineRuleSettings(
            limits.Value.MaxWordsPerSentence,
            limits.Value.MaxConnectorsPerSentence,
            EmbeddedGuidelineLists.AllowedAcronyms(),
            EmbeddedGuidelineLists.LegalJargon(),
            EmbeddedGuidelineLists.DifficultWords());
        _analyzer = new GuidelineAnalyzer(GuidelineRules.Create(settings));
        _logger = logger;
    }

    public GuidelineAnalysis Check(string text)
    {
        var analysis = _analyzer.Analyze(text);
        if (analysis.TimedOutGuidelines.Count > 0)
        {
            // Solo los códigos de pauta y la longitud: nunca el contenido del texto.
            GuidelinesTimedOut(_logger, string.Join(',', analysis.TimedOutGuidelines), text.Length);
        }

        return analysis;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pautas sin comprobar por tiempo agotado: {Guidelines} ({Characters} caracteres).")]
    private static partial void GuidelinesTimedOut(ILogger logger, string guidelines, int characters);
}
