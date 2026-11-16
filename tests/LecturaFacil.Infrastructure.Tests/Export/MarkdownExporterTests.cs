// SPDX-License-Identifier: Apache-2.0
using System.Text;
using System.Text.RegularExpressions;
using LecturaFacil.Application.Export;
using LecturaFacil.Infrastructure.Export;

namespace LecturaFacil.Infrastructure.Tests.Export;

public sealed class MarkdownExporterTests
{
    private readonly MarkdownExporter _exporter = new();

    [Fact]
    public void Exporter_DeclaresTheMarkdownFormat()
    {
        _exporter.Format.ShouldBe(ExportFormat.Markdown);
        _exporter.ContentType.ShouldBe("text/markdown; charset=utf-8");
        _exporter.FileExtension.ShouldBe("md");
    }

    [Fact]
    public void Export_UsesHeadingsOneParagraphPerSentenceGlossaryAndFooterAtTheEnd()
    {
        var markdown = Export(ExportSamples.Rent());

        markdown.ShouldBe(
            """
            # Ayudas al alquiler

            ## ¿Qué es esta ayuda?

            Es una ayuda de 250 € al mes.

            Sirve para pagar el alquiler de tu casa.

            ## ¿Cómo la pido?

            Rellena la solicitud.

            Entrégala antes del 31 de diciembre.

            ## Glosario

            - **Alquiler**: Dinero que pagas cada mes por vivir en una casa.

            ---

            *Borrador · pendiente de validación · 16 de noviembre de 2026*

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Export_EscapesImagesSoTheyAreNotLoaded()
    {
        var markdown = Export(ExportSamples.WithText("Mira ![x](https://ejemplo.com/a.png) ahora."));

        markdown.ShouldContain(@"Mira !\[x\](`https://ejemplo.com/a.png`) ahora.");
        markdown.ShouldNotContain("![");
    }

    [Fact]
    public void Export_EscapesHtml()
    {
        var markdown = Export(ExportSamples.WithText("Texto <script>alert(1)</script> &amp; <img src=x onerror=y>"));

        markdown.ShouldContain(@"Texto \<script\>alert(1)\</script\> \&amp; \<img src=x onerror=y\>");
        Regex.IsMatch(markdown, @"(?<!\\)<").ShouldBeFalse("ningún < sin escapar");
    }

    [Theory]
    [InlineData("Entra en https://ejemplo.com/datos?x=1.", "Entra en `https://ejemplo.com/datos?x=1`.")]
    [InlineData("Entra en www.ejemplo.com hoy", "Entra en `www.ejemplo.com` hoy")]
    [InlineData("Escribe a info@ejemplo.com hoy", "Escribe a `info@ejemplo.com` hoy")]
    [InlineData("Mira _www.ejemplo.com/?d=1 hoy", @"Mira \_`www.ejemplo.com/?d=1` hoy")]
    [InlineData("Mira 1https://ejemplo.com hoy", "Mira 1`https://ejemplo.com` hoy")]
    [InlineData("Mira éhttps://ejemplo.com hoy", "Mira é`https://ejemplo.com` hoy")]
    [InlineData("Mira *https://ejemplo.com* hoy", @"Mira \*`https://ejemplo.com*` hoy")]
    [InlineData("Ver [aquí](https://ejemplo.com)", @"Ver \[aquí\](`https://ejemplo.com`)")]
    public void Export_PutsAddressesInCodeSoNoViewerTurnsThemIntoLinks(string sentence, string expected) =>
        Export(ExportSamples.WithText(sentence)).ShouldContain(expected);

    [Fact]
    public void Export_UsesALongerFenceWhenTheAddressContainsBackticks()
    {
        var markdown = Export(ExportSamples.WithText("Ver https://ejemplo.com/`x`"));

        markdown.ShouldContain("Ver `` https://ejemplo.com/`x` ``");
    }

    [Theory]
    [InlineData("# No es un título", @"\# No es un título")]
    [InlineData("1. No es una lista", @"1\. No es una lista")]
    [InlineData("- Tampoco", @"\- Tampoco")]
    [InlineData("+ Tampoco", @"\+ Tampoco")]
    [InlineData("2) Tampoco", @"2\) Tampoco")]
    [InlineData("> Cita", @"\> Cita")]
    [InlineData("*negrita* y _cursiva_ y `código` y ~tachado~", @"\*negrita\* y \_cursiva\_ y \`código\` y \~tachado\~")]
    [InlineData("a | b", @"a \| b")]
    [InlineData(@"barra \ invertida", @"barra \\ invertida")]
    [InlineData("[^nota]: definición", @"\[^nota\]: definición")]
    public void Export_EscapesMarkdownSyntax(string sentence, string expected) =>
        Export(ExportSamples.WithText(sentence)).ShouldContain(expected);

    [Fact]
    public void Export_EscapesTheTitleAndTheGlossary()
    {
        var document = ExportSamples.Rent() with
        {
            Title = "<b>Título</b>",
            Glossary = [new("**Término**", "[enlace](https://ejemplo.com)")],
        };

        var markdown = Export(document);

        markdown.ShouldStartWith(@"# \<b\>Título\</b\>");
        markdown.ShouldContain(@"- **\*\*Término\*\***: \[enlace\](`https://ejemplo.com`)");
    }

    private string Export(ExportableDocument document) => Encoding.UTF8.GetString(_exporter.Export(document));
}
