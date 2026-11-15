// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Documents;

/// <summary>Errores esperados al recibir un documento. Los mensajes se muestran al usuario.</summary>
public static class DocumentErrors
{
    public const string FileTooLargeCode = "document.file_too_large";
    public const string TextTooShortCode = "document.text_too_short";
    public const string TextTooLongCode = "document.text_too_long";

    private const int BytesPerKilobyte = 1024;
    private const int BytesPerMegabyte = BytesPerKilobyte * BytesPerKilobyte;

    public static readonly Error InvalidFileType = new(
        "document.invalid_file_type",
        "El fichero no es un PDF. Solo se admiten ficheros PDF con texto.");

    public static readonly Error ScannedPdf = new(
        "document.scanned_pdf",
        "Este PDF parece escaneado. De momento solo se admiten PDF con texto.");

    public static readonly Error UnreadablePdf = new(
        "document.unreadable_pdf",
        "No se ha podido leer el PDF. Comprueba que no está dañado ni protegido con contraseña.");

    public static readonly Error PdfTooComplex = new(
        "document.pdf_too_complex",
        "El PDF tiene demasiadas páginas o tarda demasiado en leerse. Prueba con un documento más corto.");

    public static Error FileTooLarge(int maxBytes) => new(
        FileTooLargeCode,
        $"El fichero supera el tamaño máximo de {FormatSize(maxBytes)}.");

    public static Error TextTooShort(int minCharacters) => new(
        TextTooShortCode,
        string.Create(CultureInfo.InvariantCulture, $"El texto debe tener al menos {minCharacters} caracteres."));

    public static Error TextTooLong(int maxCharacters) => new(
        TextTooLongCode,
        string.Create(CultureInfo.InvariantCulture, $"El texto no puede superar {maxCharacters} caracteres."));

    private static string FormatSize(int bytes) => bytes >= BytesPerMegabyte
        ? string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)BytesPerMegabyte:0.#} MB")
        : string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)BytesPerKilobyte:0.#} KB");
}
