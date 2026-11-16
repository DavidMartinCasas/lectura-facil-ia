// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Export;

/// <summary>
/// Estrategia de exportación a un formato. Añadir un formato es añadir una implementación, sin tocar
/// <see cref="ExportDocument"/>. Solo traduce el <see cref="ExportableDocument"/>: no decide qué se exporta.
/// </summary>
public interface IDocumentExporter
{
    ExportFormat Format { get; }

    /// <summary>Tipo MIME de la descarga.</summary>
    string ContentType { get; }

    /// <summary>Extensión del fichero, sin el punto.</summary>
    string FileExtension { get; }

    /// <summary>El fichero completo. El texto se inserta siempre como texto, escapado según el formato.</summary>
    byte[] Export(ExportableDocument document);
}
