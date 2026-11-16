// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Export;

/// <summary>Errores esperados al exportar una adaptación. Los mensajes se muestran al usuario.</summary>
public static class ExportErrors
{
    public static readonly Error UnsupportedFormat = new(
        "export.unsupported_format",
        "Ese formato no se puede exportar. Elige DOCX, PDF, Markdown o texto.");

    public static readonly Error PendingWarnings = new(
        "export.pending_warnings",
        "Revisa los avisos antes de exportar");

    public static readonly Error NothingToExport = new(
        "export.nothing_to_export",
        "Este documento todavía no tiene una adaptación que exportar.");
}
