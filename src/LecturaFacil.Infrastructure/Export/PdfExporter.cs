// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Export;
using Microsoft.Extensions.Options;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace LecturaFacil.Infrastructure.Export;

/// <summary>
/// PDF (PDFsharp + MigraDoc, MIT) para enviar a validación o publicar tal cual. Misma maquetación que el DOCX, con la
/// fuente Atkinson Hyperlegible incrustada. Los encabezados tienen nivel de esquema (marcadores del PDF), el documento
/// declara su título y el idioma español, y el aviso de borrador va en el pie de cada página. El texto se añade como
/// texto: nunca como campos ni enlaces.
/// </summary>
public sealed class PdfExporter : IDocumentExporter
{
    public const string PdfLanguage = "es-ES";

    private static readonly Lock FontSetupLock = new();

    private readonly ExportOptions _options;

    public PdfExporter(IOptions<ExportOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        UseEmbeddedFonts();
    }

    public ExportFormat Format => ExportFormat.Pdf;

    public string ContentType => "application/pdf";

    public string FileExtension => "pdf";

    public byte[] Export(ExportableDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var renderer = new PdfDocumentRenderer { Document = Build(document), Language = PdfLanguage };
        renderer.RenderDocument();
        renderer.PdfDocument.Info.Creator = string.Empty;

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    /// <summary>
    /// El resolvedor de fuentes de PDFsharp es global al proceso y solo se puede registrar una vez (siempre la misma
    /// instancia); otro uso de PDFsharp en el proceso compartiría esta fuente.
    /// </summary>
    private static void UseEmbeddedFonts()
    {
        lock (FontSetupLock)
        {
            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = EmbeddedFontResolver.Instance;
                MigraDoc.PredefinedFontsAndChars.ErrorFontName = EmbeddedFontResolver.FamilyName;
            }
        }
    }

    private Document Build(ExportableDocument exportable)
    {
        var document = new Document();
        document.Info.Title = exportable.Title;
        document.Info.Author = string.Empty;
        DefineStyles(document);

        var section = document.AddSection();
        section.PageSetup = document.DefaultPageSetup.Clone();
        section.PageSetup.PageFormat = PageFormat.A4;
        var margin = Unit.FromCentimeter(_options.MarginCentimeters);
        section.PageSetup.TopMargin = margin;
        section.PageSetup.BottomMargin = margin;
        section.PageSetup.LeftMargin = margin;
        section.PageSetup.RightMargin = margin;
        section.PageSetup.FooterDistance = Unit.FromCentimeter(_options.MarginCentimeters / 2);
        section.Footers.Primary.AddParagraph().AddText(exportable.Footer);

        section.AddParagraph(exportable.Title, StyleNames.Heading1);
        foreach (var adapted in exportable.Sections)
        {
            section.AddParagraph(adapted.Question, StyleNames.Heading2);
            foreach (var sentence in adapted.Sentences)
            {
                section.AddParagraph().AddText(sentence);
            }
        }

        if (exportable.Glossary.Count > 0)
        {
            section.AddParagraph(exportable.GlossaryTitle, StyleNames.Heading2);
            foreach (var term in exportable.Glossary)
            {
                var paragraph = section.AddParagraph();
                paragraph.AddFormattedText(term.Term, TextFormat.Bold);
                paragraph.AddText($": {term.Definition}");
            }
        }

        return document;
    }

    private void DefineStyles(Document document)
    {
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = EmbeddedFontResolver.FamilyName;
        normal.Font.Size = Unit.FromPoint(_options.FontSizePoints);
        normal.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Multiple;
        normal.ParagraphFormat.LineSpacing = _options.LineSpacing;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(_options.FontSizePoints / 2);

        DefineHeading(document.Styles[StyleNames.Heading1]!, _options.TitleFontSizePoints, OutlineLevel.Level1);
        DefineHeading(document.Styles[StyleNames.Heading2]!, _options.HeadingFontSizePoints, OutlineLevel.Level2);
        document.Styles[StyleNames.Footer]!.Font.Size = Unit.FromPoint(_options.FontSizePoints);
    }

    private static void DefineHeading(Style style, double sizePoints, OutlineLevel level)
    {
        style.Font.Size = Unit.FromPoint(sizePoints);
        style.Font.Bold = true;
        style.ParagraphFormat.OutlineLevel = level;
        style.ParagraphFormat.KeepWithNext = true;
        style.ParagraphFormat.SpaceBefore = Unit.FromPoint(sizePoints);
        style.ParagraphFormat.SpaceAfter = Unit.FromPoint(sizePoints / 2);
    }
}
