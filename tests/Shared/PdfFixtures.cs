// SPDX-License-Identifier: Apache-2.0
using System.Text;

namespace LecturaFacil.Testing;

/// <summary>PDFs de prueba de <c>tests/fixtures/pdf</c> (ver su README). Sin datos personales.</summary>
public static class PdfFixtures
{
    public const string Normal = "normal.pdf";
    public const string WithoutText = "sin-texto.pdf";
    public const string Corrupt = "corrupto.pdf";

    public static byte[] Read(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "pdf", name));

    /// <summary>Un ZIP (firma <c>PK</c>) que se hará pasar por <c>documento.pdf</c>.</summary>
    public static byte[] FakePdf() => Encoding.ASCII.GetBytes("PK\u0003\u0004 esto es un zip con extension .pdf");
}
