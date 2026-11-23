// SPDX-License-Identifier: Apache-2.0
using System.Text;
using LecturaFacil.Application.Export;
using LecturaFacil.Infrastructure.Export;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LecturaFacil.Infrastructure.Tests.Export;

public sealed class PdfExporterTests
{
    private readonly PdfExporter _exporter = new(Options.Create(new ExportOptions()));

    [Fact]
    public void Exporter_DeclaresThePdfFormat()
    {
        _exporter.Format.ShouldBe(ExportFormat.Pdf);
        _exporter.ContentType.ShouldBe("application/pdf");
        _exporter.FileExtension.ShouldBe("pdf");
    }

    [Fact]
    public void Export_ContainsTheTitleSectionsGlossaryAndFooter()
    {
        using var pdf = PdfDocument.Open(_exporter.Export(ExportSamples.Rent()));

        var text = Text(pdf);
        text.ShouldContain("Ayudas al alquiler");
        text.ShouldContain("¿Qué es esta ayuda?");
        text.ShouldContain("Sirve para pagar el alquiler de tu casa.");
        text.ShouldContain("Alquiler: Dinero que pagas cada mes por vivir en una casa.");
        text.ShouldContain(ExportSamples.Footer);
    }

    [Fact]
    public void Export_PutsTheFooterOnEveryPage()
    {
        var sentences = Enumerable.Range(1, 80).Select(number => $"Esta es la frase número {number}.").ToList();
        var longDocument = ExportSamples.Rent() with { Sections = [new("¿Qué es?", sentences)] };

        using var pdf = PdfDocument.Open(_exporter.Export(longDocument));

        pdf.NumberOfPages.ShouldBeGreaterThan(1);
        pdf.GetPages().ShouldAllBe(page => Text(page).Contains(ExportSamples.Footer));
    }

    [Fact]
    public void Export_StartsWithACoverAndPutsTheContentOnTheNextPage()
    {
        using var pdf = PdfDocument.Open(_exporter.Export(ExportSamples.Rent()));

        pdf.NumberOfPages.ShouldBeGreaterThanOrEqualTo(2);
        var cover = Text(pdf.GetPage(1));
        cover.ShouldContain("Ayudas al alquiler");
        cover.ShouldContain("Adaptación a lectura fácil");
        cover.ShouldContain("Borrador · pendiente de validación");
        cover.ShouldContain("16 de noviembre de 2026");
        cover.ShouldNotContain("¿Qué es esta ayuda?");
        Text(pdf.GetPage(2)).ShouldContain("¿Qué es esta ayuda?");
    }

    [Fact]
    public void Export_LowersTheCoverTitleWellBelowTheTopOfThePage()
    {
        using var pdf = PdfDocument.Open(_exporter.Export(ExportSamples.Rent()));

        static double TitleTop(Page page) => page.GetWords().First(word => word.Text == "Ayudas").BoundingBox.Top;
        var cover = pdf.GetPage(1);
        var content = pdf.GetPage(2);

        // El título de la portada queda al menos 5 cm (unos 140 puntos) más abajo que el del contenido.
        (TitleTop(content) - TitleTop(cover)).ShouldBeGreaterThan(140);
    }

    [Fact]
    public void Export_EmbedsAtkinsonHyperlegibleAtLeast14Points()
    {
        using var pdf = PdfDocument.Open(_exporter.Export(ExportSamples.Rent()));

        var letters = pdf.GetPages().SelectMany(page => page.Letters).ToList();
        letters.ShouldAllBe(letter => letter.FontName != null && letter.FontName.Contains("Atkinson Hyperlegible", StringComparison.Ordinal));
        letters.Min(letter => letter.PointSize).ShouldBeGreaterThanOrEqualTo(14);
    }

    [Fact]
    public void Export_UsesLineSpacingOfAtLeastOneAndAHalfAndLeftAlignment()
    {
        var longSentence = string.Join(' ', Enumerable.Repeat("Esta frase es larga para que ocupe varias líneas.", 6));
        using var pdf = PdfDocument.Open(_exporter.Export(ExportSamples.WithText(longSentence)));

        // La primera página es la portada: el contenido empieza en la segunda.
        var words = pdf.GetPage(2).GetWords().ToList();
        var lines = words
            .Where(word => word.Letters[0].PointSize == 14 && !ExportSamples.Footer.Contains(word.Text, StringComparison.Ordinal))
            .GroupBy(word => Math.Round(word.BoundingBox.Bottom, 1))
            .OrderByDescending(line => line.Key)
            .Select(line => (Baseline: line.Key, Start: line.Min(word => word.BoundingBox.Left), End: line.Max(word => word.BoundingBox.Right)))
            .ToList();
        lines.Count.ShouldBeGreaterThanOrEqualTo(3);

        // Interlineado: entre dos líneas seguidas de la misma frase hay al menos 1,5 veces el tamaño de la letra.
        for (var index = 1; index < lines.Count; index++)
        {
            (lines[index - 1].Baseline - lines[index].Baseline).ShouldBeGreaterThanOrEqualTo(1.5 * 14 - 0.5);
        }

        // Alineación a la izquierda: todas las líneas empiezan en el margen y no se estiran hasta el derecho.
        lines.Select(line => Math.Round(line.Start)).Distinct().ShouldHaveSingleItem();
        lines.Select(line => Math.Round(line.End)).Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Export_DeclaresTitleAndSpanishLanguageWithoutInternalMetadata()
    {
        var bytes = _exporter.Export(ExportSamples.Rent());
        using var pdf = PdfDocument.Open(bytes);

        pdf.Information.Title.ShouldBe("Ayudas al alquiler");
        pdf.Information.Author.ShouldBeNullOrEmpty();
        pdf.Information.Creator.ShouldBeNullOrEmpty();
        Encoding.Latin1.GetString(bytes).ShouldContain("/Lang(es-ES)");
    }

    [Fact]
    public void Export_HasBookmarksForTheHeadings()
    {
        using var pdf = PdfDocument.Open(_exporter.Export(ExportSamples.Rent()));

        pdf.TryGetBookmarks(out var bookmarks).ShouldBeTrue();
        var title = bookmarks.Roots.ShouldHaveSingleItem();
        title.Title.ShouldBe("Ayudas al alquiler");
        title.Children.Select(child => child.Title).ShouldBe(["¿Qué es esta ayuda?", "¿Cómo la pido?", "Glosario"]);
    }

    [Fact]
    public void Export_KeepsMarkupAsTextWithoutLinks()
    {
        using var pdf = PdfDocument.Open(_exporter.Export(ExportSamples.WithText("Mira [x](https://ejemplo.com) <b>y</b>.")));

        Text(pdf).ShouldContain("Mira [x](https://ejemplo.com) <b>y</b>.");
        pdf.GetPages().SelectMany(page => page.GetHyperlinks()).ShouldBeEmpty();
    }

    private static string Text(PdfDocument pdf) => string.Join(' ', pdf.GetPages().Select(Text));

    // MigraDoc coloca cada palabra por separado: el texto de la página se reconstruye uniendo las palabras.
    private static string Text(Page page) => string.Join(' ', page.GetWords().Select(word => word.Text));
}
