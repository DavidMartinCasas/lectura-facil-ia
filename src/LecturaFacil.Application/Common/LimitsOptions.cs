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

    /// <summary>Conectores que abren otra oración ("cuyo", "siempre que"…) a partir de los que una frase une varias ideas.</summary>
    [Range(1, 10, ErrorMessage = "Limits:MaxConnectorsPerSentence debe estar entre {1} y {2}.")]
    public int MaxConnectorsPerSentence { get; set; } = 1;

    /// <summary>Hallazgos que se guardan como máximo por documento (los primeros del texto). Acota las filas por petición.</summary>
    [Range(10, 5_000, ErrorMessage = "Limits:MaxFindingsPerDocument debe estar entre {1} y {2}.")]
    public int MaxFindingsPerDocument { get; set; } = 500;

    [Range(256, 32_768, ErrorMessage = "Limits:MaxOutputTokens debe estar entre {1} y {2}.")]
    public int MaxOutputTokens { get; set; } = 4_096;

    [Range(5, 600, ErrorMessage = "Limits:LlmTimeoutSeconds debe estar entre {1} y {2}.")]
    public int LlmTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Caracteres máximos de cada parte de un texto que se adapta por separado. Un texto más largo se divide por párrafos
    /// y las adaptaciones se unen. Debe caber en el contexto del modelo adaptador junto con el prompt y la respuesta.
    /// </summary>
    [Range(500, 200_000, ErrorMessage = "Limits:MaxCharactersPerAdaptationChunk debe estar entre {1} y {2}.")]
    public int MaxCharactersPerAdaptationChunk { get; set; } = 6_000;

    /// <summary>Partes como máximo en que se divide un documento para adaptarlo. Acota las llamadas al modelo (OWASP LLM10).</summary>
    [Range(1, 100, ErrorMessage = "Limits:MaxAdaptationChunksPerDocument debe estar entre {1} y {2}.")]
    public int MaxAdaptationChunksPerDocument { get; set; } = 10;

    /// <summary>Documentos que se adaptan a la vez. Por defecto 1: el servidor local atiende una petición cada vez.</summary>
    [Range(1, 16, ErrorMessage = "Limits:AdaptationConcurrency debe estar entre {1} y {2}.")]
    public int AdaptationConcurrency { get; set; } = 1;

    /// <summary>Cada cuánto se consulta el estado de un documento para enviar sus cambios por SSE.</summary>
    [Range(100, 60_000, ErrorMessage = "Limits:DocumentEventsPollMilliseconds debe estar entre {1} y {2}.")]
    public int DocumentEventsPollMilliseconds { get; set; } = 1_000;

    /// <summary>Tiempo máximo que se mantiene abierta una conexión SSE de progreso. El cliente puede volver a conectarse.</summary>
    [Range(10, 3_600, ErrorMessage = "Limits:DocumentEventsMaxSeconds debe estar entre {1} y {2}.")]
    public int DocumentEventsMaxSeconds { get; set; } = 600;

    public TextLengthLimits TextLengthLimits => new(MinCharacters, MaxCharacters);
}
