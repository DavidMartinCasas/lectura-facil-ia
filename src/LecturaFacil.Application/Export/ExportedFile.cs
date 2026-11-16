// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Export;

/// <summary>Fichero listo para descargar.</summary>
/// <param name="FileName">Nombre saneado (<see cref="ExportFileName"/>), con su extensión.</param>
public sealed record ExportedFile(string FileName, string ContentType, byte[] Content);
