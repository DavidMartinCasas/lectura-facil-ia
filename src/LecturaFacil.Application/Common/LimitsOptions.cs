// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Common;

/// <summary>Límites de entrada, de las pautas y de las llamadas al LLM. Los valores por defecto son conservadores.</summary>
public sealed class LimitsOptions
{
    public const string SectionName = "Limits";

    [Range(1_024, 50 * 1024 * 1024, ErrorMessage = "Limits:MaxFileSizeBytes debe estar entre {1} y {2}.")]
    public int MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    [Range(100, 200_000, ErrorMessage = "Limits:MaxCharacters debe estar entre {1} y {2}.")]
    public int MaxCharacters { get; set; } = 20_000;

    /// <summary>Caracteres mínimos del texto de entrada. Su máximo es el mínimo de <see cref="MaxCharacters"/>, así nunca lo supera.</summary>
    [Range(1, 100, ErrorMessage = "Limits:MinCharacters debe estar entre {1} y {2}.")]
    public int MinCharacters { get; set; } = 50;

    [Range(1, 2_000, ErrorMessage = "Limits:MaxPdfPages debe estar entre {1} y {2}.")]
    public int MaxPdfPages { get; set; } = 100;

    [Range(1, 120, ErrorMessage = "Limits:PdfExtractionTimeoutSeconds debe estar entre {1} y {2}.")]
    public int PdfExtractionTimeoutSeconds { get; set; } = 15;

    [Range(1, 64, ErrorMessage = "Limits:MaxConcurrentPdfExtractions debe estar entre {1} y {2}.")]
    public int MaxConcurrentPdfExtractions { get; set; } = 2;

    [Range(5, 50, ErrorMessage = "Limits:MaxWordsPerSentence debe estar entre {1} y {2}.")]
    public int MaxWordsPerSentence { get; set; } = 15;

    [Range(256, 32_768, ErrorMessage = "Limits:MaxOutputTokens debe estar entre {1} y {2}.")]
    public int MaxOutputTokens { get; set; } = 4_096;

    [Range(5, 600, ErrorMessage = "Limits:LlmTimeoutSeconds debe estar entre {1} y {2}.")]
    public int LlmTimeoutSeconds { get; set; } = 120;

    public TextLengthLimits TextLengthLimits => new(MinCharacters, MaxCharacters);
}
