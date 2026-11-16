// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Export;

namespace LecturaFacil.Application.Tests.Export;

public sealed class ExportFileNameTests
{
    [Theory]
    [InlineData("Ayudas al alquiler", "ayudas-al-alquiler.docx")]
    [InlineData("¿Cómo pido la ayuda? Año 2026", "como-pido-la-ayuda-ano-2026.docx")]
    [InlineData("../../etc/passwd", "etc-passwd.docx")]
    [InlineData("C:\\Windows\\system32", "c-windows-system32.docx")]
    [InlineData("informe\r\nContent-Type: text/html", "informe-content-type-text-html.docx")]
    [InlineData("<script>alert(1)</script>", "script-alert-1-script.docx")]
    [InlineData("¿¿??", "documento.docx")]
    [InlineData("", "documento.docx")]
    public void Create_KeepsOnlyLowercaseLettersDigitsAndHyphens(string title, string expected) =>
        ExportFileName.Create(title, "docx").ShouldBe(expected);

    [Fact]
    public void Create_TruncatesLongTitlesWithoutLeavingATrailingHyphen()
    {
        var title = string.Join(' ', Enumerable.Repeat("palabra", 30));

        var name = ExportFileName.Create(title, "md");

        name.Length.ShouldBeLessThanOrEqualTo(ExportFileName.MaxBaseLength + ".md".Length);
        name.ShouldEndWith("palabra.md");
        name.ShouldNotContain("-.");
    }
}
