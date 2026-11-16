// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;

namespace LecturaFacil.Infrastructure.Export;

/// <summary>
/// Maquetación de lectura fácil de los ficheros DOCX y PDF (US-14). Los mínimos de los rangos son los de la historia: letra
/// de al menos 14 pt e interlineado de al menos 1,5. El texto siempre va alineado a la izquierda, sin justificar.
/// </summary>
public sealed class ExportOptions
{
    public const string SectionName = "Export";

    /// <summary>Tamaño de la letra del texto, del glosario y del pie, en puntos.</summary>
    [Range(14, 28, ErrorMessage = "Export:FontSizePoints debe estar entre {1} y {2}.")]
    public double FontSizePoints { get; set; } = 14;

    /// <summary>Tamaño del título del documento (encabezado de nivel 1), en puntos.</summary>
    [Range(14, 48, ErrorMessage = "Export:TitleFontSizePoints debe estar entre {1} y {2}.")]
    public double TitleFontSizePoints { get; set; } = 22;

    /// <summary>Tamaño de las preguntas de cada sección y del título del glosario (encabezado de nivel 2), en puntos.</summary>
    [Range(14, 36, ErrorMessage = "Export:HeadingFontSizePoints debe estar entre {1} y {2}.")]
    public double HeadingFontSizePoints { get; set; } = 18;

    /// <summary>Interlineado, como múltiplo de la altura de la línea.</summary>
    [Range(1.5, 3.0, ErrorMessage = "Export:LineSpacing debe estar entre {1} y {2}.")]
    public double LineSpacing { get; set; } = 1.5;

    /// <summary>Márgenes de la página (A4), en centímetros.</summary>
    [Range(1.5, 5.0, ErrorMessage = "Export:MarginCentimeters debe estar entre {1} y {2}.")]
    public double MarginCentimeters { get; set; } = 2.5;

    /// <summary>
    /// Fuente sin serifa del DOCX. Un DOCX no lleva la fuente dentro: se usa una que tengan casi todos los equipos (Word
    /// la tiene y LibreOffice la sustituye por una equivalente sin serifa). El PDF siempre lleva Atkinson Hyperlegible.
    /// </summary>
    [Required(ErrorMessage = "Export:DocxFontFamily es obligatorio.")]
    [RegularExpression(@"^[A-Za-z0-9 ]{1,64}$", ErrorMessage = "Export:DocxFontFamily solo admite letras, cifras y espacios (64 como mucho).")]
    public string DocxFontFamily { get; set; } = "Arial";
}
