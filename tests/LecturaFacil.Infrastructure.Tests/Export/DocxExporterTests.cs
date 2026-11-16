// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using LecturaFacil.Application.Export;
using LecturaFacil.Infrastructure.Export;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Tests.Export;

public sealed class DocxExporterTests
{
    private readonly DocxExporter _exporter = new(Options.Create(new ExportOptions()));

    [Fact]
    public void Exporter_DeclaresTheDocxFormat()
    {
        _exporter.Format.ShouldBe(ExportFormat.Docx);
        _exporter.ContentType.ShouldBe("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        _exporter.FileExtension.ShouldBe("docx");
    }

    [Fact]
    public void Export_ProducesAValidDocumentThatOpenXmlCanOpen()
    {
        using var package = Open(ExportSamples.Rent());

        new OpenXmlValidator().Validate(package, TestContext.Current.CancellationToken).ShouldBeEmpty();
    }

    [Fact]
    public void Export_UsesRealHeadingStylesForTheTitleAndTheQuestions()
    {
        using var package = Open(ExportSamples.Rent());

        var paragraphs = Paragraphs(package);
        paragraphs.ShouldContain(("Heading1", "Ayudas al alquiler"));
        paragraphs.ShouldContain(("Heading2", "¿Qué es esta ayuda?"));
        paragraphs.ShouldContain(("Heading2", "¿Cómo la pido?"));
        paragraphs.ShouldContain(("Heading2", "Glosario"));

        var styles = package.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<Style>().ToList();
        var heading1 = styles.Single(style => style.StyleId == "Heading1");
        heading1.StyleName!.Val!.Value.ShouldBe("heading 1");
        heading1.StyleParagraphProperties!.OutlineLevel!.Val!.Value.ShouldBe(0);
        styles.Single(style => style.StyleId == "Heading2").StyleParagraphProperties!.OutlineLevel!.Val!.Value.ShouldBe(1);
    }

    [Fact]
    public void Export_WritesOneSentencePerParagraphAndTheGlossaryAtTheEnd()
    {
        using var package = Open(ExportSamples.Rent());

        var texts = Paragraphs(package).Select(paragraph => paragraph.Text).ToList();
        texts.ShouldBe(
        [
            "Ayudas al alquiler",
            "¿Qué es esta ayuda?",
            "Es una ayuda de 250 € al mes.",
            "Sirve para pagar el alquiler de tu casa.",
            "¿Cómo la pido?",
            "Rellena la solicitud.",
            "Entrégala antes del 31 de diciembre.",
            "Glosario",
            "Alquiler: Dinero que pagas cada mes por vivir en una casa.",
        ]);
    }

    [Fact]
    public void Export_DeclaresTitleAndSpanishLanguage()
    {
        using var package = Open(ExportSamples.Rent());

        package.PackageProperties.Title.ShouldBe("Ayudas al alquiler");
        package.PackageProperties.Language.ShouldBe("es-ES");
        package.PackageProperties.Creator.ShouldBeNull();
        var defaults = package.MainDocumentPart!.StyleDefinitionsPart!.Styles!.DocDefaults!.RunPropertiesDefault!.RunPropertiesBaseStyle!;
        defaults.Languages!.Val!.Value.ShouldBe("es-ES");
    }

    [Fact]
    public void Export_UsesALargeSansSerifFontWideLineSpacingAndLeftAlignment()
    {
        using var package = Open(ExportSamples.Rent());

        var defaults = package.MainDocumentPart!.StyleDefinitionsPart!.Styles!.DocDefaults!;
        var run = defaults.RunPropertiesDefault!.RunPropertiesBaseStyle!;
        run.RunFonts!.Ascii!.Value.ShouldBe("Arial");
        int.Parse(run.FontSize!.Val!.Value!, CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(28);
        var paragraph = defaults.ParagraphPropertiesDefault!.ParagraphPropertiesBaseStyle!;
        int.Parse(paragraph.SpacingBetweenLines!.Line!.Value!, CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(360);
        paragraph.SpacingBetweenLines.LineRule!.Value.ShouldBe(LineSpacingRuleValues.Auto);
        paragraph.Justification!.Val!.Value.ShouldBe(JustificationValues.Left);
        Body(package).Descendants<Justification>().ShouldBeEmpty();
    }

    [Fact]
    public void Export_PutsTheDraftNoticeInTheFooterOfEveryPage()
    {
        using var package = Open(ExportSamples.Rent());

        var main = package.MainDocumentPart!;
        var reference = Body(package).Elements<SectionProperties>().Single().Elements<FooterReference>().Single();
        reference.Type!.Value.ShouldBe(HeaderFooterValues.Default);
        var footer = (FooterPart)main.GetPartById(reference.Id!.Value!);
        footer.Footer!.InnerText.ShouldBe(ExportSamples.Footer);
    }

    [Fact]
    public void Export_InsertsMarkupAsPlainTextWithoutFieldsOrHyperlinks()
    {
        using var package = Open(ExportSamples.WithText("Mira { HYPERLINK \"https://ejemplo.com\" } y <w:fldSimple w:instr=\"DATE\"/> y [x](https://ejemplo.com)."));

        var body = Body(package);
        body.InnerText.ShouldContain("Mira { HYPERLINK \"https://ejemplo.com\" } y <w:fldSimple w:instr=\"DATE\"/> y [x](https://ejemplo.com).");
        body.Descendants<Hyperlink>().ShouldBeEmpty();
        body.Descendants<SimpleField>().ShouldBeEmpty();
        body.Descendants<FieldCode>().ShouldBeEmpty();
        package.MainDocumentPart!.HyperlinkRelationships.ShouldBeEmpty();
    }

    [Fact]
    public void Export_UsesTheConfiguredLayout()
    {
        var exporter = new DocxExporter(Options.Create(new ExportOptions { FontSizePoints = 16, LineSpacing = 2, DocxFontFamily = "Verdana" }));
        using var stream = new MemoryStream(exporter.Export(ExportSamples.Rent()));
        using var package = WordprocessingDocument.Open(stream, isEditable: false);

        var run = package.MainDocumentPart!.StyleDefinitionsPart!.Styles!.DocDefaults!.RunPropertiesDefault!.RunPropertiesBaseStyle!;
        run.FontSize!.Val!.Value.ShouldBe("32");
        run.RunFonts!.Ascii!.Value.ShouldBe("Verdana");
        package.MainDocumentPart.StyleDefinitionsPart.Styles.DocDefaults.ParagraphPropertiesDefault!.ParagraphPropertiesBaseStyle!
            .SpacingBetweenLines!.Line!.Value.ShouldBe("480");
    }

    private static Body Body(WordprocessingDocument package) => package.MainDocumentPart!.Document!.Body!;

    private static List<(string? Style, string Text)> Paragraphs(WordprocessingDocument package) =>
        [.. Body(package).Elements<Paragraph>().Select(paragraph => (paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value, paragraph.InnerText))];

    private WordprocessingDocument Open(ExportableDocument document) =>
        WordprocessingDocument.Open(new MemoryStream(_exporter.Export(document)), isEditable: false);
}
