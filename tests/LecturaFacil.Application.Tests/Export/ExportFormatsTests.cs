// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Export;

namespace LecturaFacil.Application.Tests.Export;

public sealed class ExportFormatsTests
{
    [Theory]
    [InlineData("docx", ExportFormat.Docx)]
    [InlineData("pdf", ExportFormat.Pdf)]
    [InlineData("md", ExportFormat.Markdown)]
    [InlineData("txt", ExportFormat.PlainText)]
    public void TryParse_AcceptsTheAllowList(string value, ExportFormat expected)
    {
        ExportFormats.TryParse(value, out var format).ShouldBeTrue();
        format.ShouldBe(expected);
    }

    [Theory]
    [InlineData("exe")]
    [InlineData("Docx")]
    [InlineData("DOCX")]
    [InlineData("markdown")]
    [InlineData("0")]
    [InlineData("PlainText")]
    [InlineData(null)]
    public void TryParse_RejectsEverythingElse(string? value) =>
        ExportFormats.TryParse(value, out _).ShouldBeFalse();
}
