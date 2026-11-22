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

    /// <summary>Palabras como mucho del título del documento y de cada sección de la adaptación (pauta TITULOS_CLAROS).</summary>
    [Range(3, 20, ErrorMessage = "Limits:MaxWordsPerHeading debe estar entre {1} y {2}.")]
    public int MaxWordsPerHeading { get; set; } = 8;

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

    /// <summary>
    /// Nuevas adaptaciones que se piden como mucho cuando la adaptación incumple pautas de gravedad alta (US-07). Cada
    /// una vuelve a adaptar el documento entero y se guarda la mejor versión. 0 desactiva el reintento (OWASP LLM10).
    /// </summary>
    [Range(0, 3, ErrorMessage = "Limits:MaxAdaptationRetries debe estar entre {1} y {2}.")]
    public int MaxAdaptationRetries { get; set; } = 1;

    /// <summary>
    /// Caracteres como mucho del original más la adaptación que se envían juntos al verificador del significado (US-11).
    /// Si se superan, no se llama al modelo y todas las cifras del original quedan para revisar (OWASP LLM10).
    /// </summary>
    [Range(1_000, 400_000, ErrorMessage = "Limits:MaxCharactersPerVerification debe estar entre {1} y {2}.")]
    public int MaxCharactersPerVerification { get; set; } = 50_000;

    /// <summary>Datos clave conservados que se guardan como mucho por versión. Los avisos se guardan siempre todos.</summary>
    [Range(10, 1_000, ErrorMessage = "Limits:MaxKeyFactsPerVersion debe estar entre {1} y {2}.")]
    public int MaxKeyFactsPerVersion { get; set; } = 100;

    /// <summary>
    /// Documentos que puede enviar cada usuario por hora en <c>POST /api/documents</c> (US-16): cada uno genera llamadas
    /// al modelo (OWASP API4 y LLM10). Al superarlo, 429 con <c>Retry-After</c>.
    /// </summary>
    [Range(1, 1_000, ErrorMessage = "Limits:AdaptationsPerHour debe estar entre {1} y {2}.")]
    public int AdaptationsPerHour { get; set; } = 10;

    /// <summary>Documentos que se adaptan a la vez. Por defecto 1: el servidor local atiende una petición cada vez.</summary>
    [Range(1, 16, ErrorMessage = "Limits:AdaptationConcurrency debe estar entre {1} y {2}.")]
    public int AdaptationConcurrency { get; set; } = 1;

    /// <summary>Cada cuánto se consulta el estado de un documento para enviar sus cambios por SSE.</summary>
    [Range(100, 60_000, ErrorMessage = "Limits:DocumentEventsPollMilliseconds debe estar entre {1} y {2}.")]
    public int DocumentEventsPollMilliseconds { get; set; } = 1_000;

    /// <summary>Tiempo máximo que se mantiene abierta una conexión SSE de progreso. El cliente puede volver a conectarse.</summary>
    [Range(10, 3_600, ErrorMessage = "Limits:DocumentEventsMaxSeconds debe estar entre {1} y {2}.")]
    public int DocumentEventsMaxSeconds { get; set; } = 600;

    /// <summary>Documentos por página que puede pedir como mucho <c>GET /api/documents</c>. Acota la respuesta.</summary>
    [Range(10, 1_000, ErrorMessage = "Limits:MaxDocumentsPerList debe estar entre {1} y {2}.")]
    public int MaxDocumentsPerList { get; set; } = 100;

    /// <summary>
    /// Caracteres como mucho de una adaptación editada a mano (título, preguntas, frases y glosario) en
    /// <c>PUT /api/documents/{id}/versions/{n}</c>. Una adaptación ocupa más que el original porque explica más.
    /// </summary>
    [Range(1_000, 400_000, ErrorMessage = "Limits:MaxEditedCharacters debe estar entre {1} y {2}.")]
    public int MaxEditedCharacters { get; set; } = 40_000;

    /// <summary>
    /// Exportaciones que puede pedir cada usuario por minuto en <c>POST /api/documents/{id}/export</c> (US-14, US-16).
    /// Generar un DOCX o un PDF cuesta CPU y memoria.
    /// </summary>
    [Range(1, 1_000, ErrorMessage = "Limits:ExportsPerMinute debe estar entre {1} y {2}.")]
    public int ExportsPerMinute { get; set; } = 30;

    public TextLengthLimits TextLengthLimits => new(MinCharacters, MaxCharacters);
}
