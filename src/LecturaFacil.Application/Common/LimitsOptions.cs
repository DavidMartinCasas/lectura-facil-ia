// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;

namespace LecturaFacil.Application.Common;

/// <summary>Límites de entrada, de las pautas y de las llamadas al LLM. Los valores por defecto son conservadores.</summary>
public sealed class LimitsOptions
{
    public const string SectionName = "Limits";

    [Range(1_024, 50 * 1024 * 1024, ErrorMessage = "Limits:MaxFileSizeBytes debe estar entre {1} y {2}.")]
    public int MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    [Range(100, 200_000, ErrorMessage = "Limits:MaxCharacters debe estar entre {1} y {2}.")]
    public int MaxCharacters { get; set; } = 20_000;

    [Range(5, 50, ErrorMessage = "Limits:MaxWordsPerSentence debe estar entre {1} y {2}.")]
    public int MaxWordsPerSentence { get; set; } = 15;

    [Range(256, 32_768, ErrorMessage = "Limits:MaxOutputTokens debe estar entre {1} y {2}.")]
    public int MaxOutputTokens { get; set; } = 4_096;

    [Range(5, 600, ErrorMessage = "Limits:LlmTimeoutSeconds debe estar entre {1} y {2}.")]
    public int LlmTimeoutSeconds { get; set; } = 120;
}
