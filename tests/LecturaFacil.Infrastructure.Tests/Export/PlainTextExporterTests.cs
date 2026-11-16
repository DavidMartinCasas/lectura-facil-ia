// SPDX-License-Identifier: Apache-2.0
using System.Text;
using LecturaFacil.Application.Export;
using LecturaFacil.Infrastructure.Export;

namespace LecturaFacil.Infrastructure.Tests.Export;

public sealed class PlainTextExporterTests
{
    private readonly PlainTextExporter _exporter = new();

    [Fact]
    public void Exporter_DeclaresTheTxtFormat()
    {
        _exporter.Format.ShouldBe(ExportFormat.PlainText);
        _exporter.ContentType.ShouldBe("text/plain; charset=utf-8");
        _exporter.FileExtension.ShouldBe("txt");
    }

    [Fact]
    public void Export_WritesTitleSectionsOneSentencePerLineGlossaryAndFooterAtTheEnd()
    {
        var text = Export(ExportSamples.Rent());

        text.ShouldBe(
            """
            Ayudas al alquiler

            ¿Qué es esta ayuda?
            Es una ayuda de 250 € al mes.
            Sirve para pagar el alquiler de tu casa.

            ¿Cómo la pido?
            Rellena la solicitud.
            Entrégala antes del 31 de diciembre.

            Glosario
            Alquiler: Dinero que pagas cada mes por vivir en una casa.

            Borrador · pendiente de validación · 16 de noviembre de 2026

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Export_WithoutGlossary_OmitsTheGlossaryHeading()
    {
        var text = Export(ExportSamples.Rent() with { Glossary = [] });

        text.ShouldNotContain("Glosario");
        text.ShouldEndWith("Borrador · pendiente de validación · 16 de noviembre de 2026\n");
    }

    [Fact]
    public void Export_KeepsMarkupAsLiteralText()
    {
        var text = Export(ExportSamples.WithText("Mira ![x](https://ejemplo.com/a.png) y <script>alert(1)</script>."));

        text.ShouldContain("Mira ![x](https://ejemplo.com/a.png) y <script>alert(1)</script>.");
    }

    [Fact]
    public void Export_UsesUtf8WithoutByteOrderMark()
    {
        var bytes = _exporter.Export(ExportSamples.Rent());

        bytes[..3].ShouldNotBe(Encoding.UTF8.Preamble.ToArray());
        Encoding.UTF8.GetString(bytes).ShouldStartWith("Ayudas al alquiler");
    }

    private string Export(ExportableDocument document) => Encoding.UTF8.GetString(_exporter.Export(document));
}
