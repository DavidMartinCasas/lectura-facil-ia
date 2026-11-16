// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Evals.Dataset;

/// <summary>Convierte el texto plano de una adaptación profesional en el contenido estructurado que verifica la aplicación.</summary>
public static class ProfessionalAdaptation
{
    /// <summary>
    /// La primera línea hace de título y de pregunta de una única sección; el resto de líneas son sus frases. Todo el
    /// texto llega al verificador y no se añade ninguna palabra (ni cifra) que no esté en la adaptación.
    /// </summary>
    public static AdaptedContent ToContent(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sentences = lines.Length > 1 ? lines[1..] : lines;
        return AdaptedContent.TryCreate(lines[0], [new AdaptedSectionDraft(lines[0], sentences)], [])
            ?? throw new ArgumentException("The professional adaptation has no text.", nameof(text));
    }
}
