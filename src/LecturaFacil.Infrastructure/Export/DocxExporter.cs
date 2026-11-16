// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LecturaFacil.Application.Export;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Export;

/// <summary>
/// DOCX (Open XML SDK), el formato principal para seguir editando. Maquetación de lectura fácil: fuente sin serifa
/// grande, interlineado amplio, texto alineado a la izquierda y una frase por párrafo. El título y las preguntas usan los
/// estilos de encabezado de Word (navegables con un lector de pantalla), el documento declara su título y el idioma
/// español, y el aviso de borrador va en el pie de cada página. Todo el texto se inserta como texto: nunca como campos,
/// hipervínculos ni macros.
/// </summary>
public sealed class DocxExporter(IOptions<ExportOptions> options) : IDocumentExporter
{
    public const string Heading1StyleId = "Heading1";
    public const string Heading2StyleId = "Heading2";
    public const string FooterStyleId = "Footer";
    public const string WordLanguage = "es-ES";

    private const int TwipsPerPoint = 20;
    private const double TwipsPerCentimeter = 1440 / 2.54;
    private const int A4WidthTwips = 11906;
    private const int A4HeightTwips = 16838;

    // Word expresa el interlineado "múltiple" en 240avos de línea.
    private const int SingleLineSpacing = 240;

    private readonly ExportOptions _options = options.Value;

    public ExportFormat Format => ExportFormat.Docx;

    public string ContentType => "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public string FileExtension => "docx";

    public byte[] Export(ExportableDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            package.PackageProperties.Title = document.Title;
            package.PackageProperties.Language = WordLanguage;

            var main = package.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = Styles();

            var footer = main.AddNewPart<FooterPart>();
            footer.Footer = new Footer(TextParagraph(FooterStyleId, document.Footer));

            main.Document = new Document(Body(document, main.GetIdOfPart(footer)));
        }

        return stream.ToArray();
    }

    private Body Body(ExportableDocument document, string footerId)
    {
        var body = new Body(TextParagraph(Heading1StyleId, document.Title));
        foreach (var section in document.Sections)
        {
            body.Append(TextParagraph(Heading2StyleId, section.Question));
            body.Append(section.Sentences.Select(sentence => TextParagraph(null, sentence)));
        }

        if (document.Glossary.Count > 0)
        {
            body.Append(TextParagraph(Heading2StyleId, document.GlossaryTitle));
            body.Append(document.Glossary.Select(term => new Paragraph(
                TextRun(term.Term, bold: true),
                TextRun($": {term.Definition}", bold: false))));
        }

        body.Append(SectionProperties(footerId));
        return body;
    }

    private SectionProperties SectionProperties(string footerId)
    {
        var margin = (int)Math.Round(_options.MarginCentimeters * TwipsPerCentimeter);
        return new SectionProperties(
            new FooterReference { Type = HeaderFooterValues.Default, Id = footerId },
            new PageSize { Width = A4WidthTwips, Height = A4HeightTwips },
            new PageMargin
            {
                Top = margin,
                Bottom = margin,
                Left = (uint)margin,
                Right = (uint)margin,
                Header = (uint)(margin / 2),
                Footer = (uint)(margin / 2),
            });
    }

    private Styles Styles()
    {
        var font = _options.DocxFontFamily;
        var defaults = new DocDefaults(
            new RunPropertiesDefault(new RunProperties(
                new RunFonts { Ascii = font, HighAnsi = font, ComplexScript = font, EastAsia = font },
                new FontSize { Val = HalfPoints(_options.FontSizePoints) },
                new FontSizeComplexScript { Val = HalfPoints(_options.FontSizePoints) },
                new Languages { Val = WordLanguage, EastAsia = WordLanguage, Bidi = WordLanguage })),
            new ParagraphPropertiesDefault(new ParagraphProperties(
                new SpacingBetweenLines
                {
                    Line = ((int)Math.Round(_options.LineSpacing * SingleLineSpacing)).ToString(CultureInfo.InvariantCulture),
                    LineRule = LineSpacingRuleValues.Auto,
                    After = Twips(_options.FontSizePoints / 2),
                },
                new Justification { Val = JustificationValues.Left })));

        return new Styles(
            defaults,
            new Style(new StyleName { Val = "Normal" }, new PrimaryStyle()) { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true },
            HeadingStyle(Heading1StyleId, "heading 1", outlineLevel: 0, _options.TitleFontSizePoints),
            HeadingStyle(Heading2StyleId, "heading 2", outlineLevel: 1, _options.HeadingFontSizePoints),
            new Style(
                new StyleName { Val = "footer" },
                new BasedOn { Val = "Normal" },
                new UIPriority { Val = 99 })
            { Type = StyleValues.Paragraph, StyleId = FooterStyleId });
    }

    /// <summary>Estilo con el nombre interno de Word ("heading 1"…), que es el que reconocen el navegador y los lectores de pantalla.</summary>
    private Style HeadingStyle(string styleId, string name, int outlineLevel, double sizePoints) =>
        new(
            new StyleName { Val = name },
            new BasedOn { Val = "Normal" },
            new NextParagraphStyle { Val = "Normal" },
            new UIPriority { Val = 9 },
            new PrimaryStyle(),
            new StyleParagraphProperties(
                new KeepNext(),
                new SpacingBetweenLines { Before = Twips(sizePoints), After = Twips(sizePoints / 2) },
                new OutlineLevel { Val = outlineLevel }),
            new StyleRunProperties(
                new Bold(),
                new BoldComplexScript(),
                new FontSize { Val = HalfPoints(sizePoints) },
                new FontSizeComplexScript { Val = HalfPoints(sizePoints) }))
        {
            Type = StyleValues.Paragraph,
            StyleId = styleId,
        };

    private static Paragraph TextParagraph(string? styleId, string text)
    {
        var paragraph = new Paragraph();
        if (styleId is not null)
        {
            paragraph.Append(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
        }

        paragraph.Append(TextRun(text, bold: false));
        return paragraph;
    }

    private static Run TextRun(string text, bool bold)
    {
        var run = new Run();
        if (bold)
        {
            run.Append(new RunProperties(new Bold(), new BoldComplexScript()));
        }

        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static string HalfPoints(double points) => ((int)Math.Round(points * 2)).ToString(CultureInfo.InvariantCulture);

    private static string Twips(double points) => ((int)Math.Round(points * TwipsPerPoint)).ToString(CultureInfo.InvariantCulture);
}
