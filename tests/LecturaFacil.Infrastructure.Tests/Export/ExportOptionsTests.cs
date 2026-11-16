// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Export;

namespace LecturaFacil.Infrastructure.Tests.Export;

public sealed class ExportOptionsTests
{
    [Fact]
    public void Defaults_AreValidAndFollowEasyReadLayout()
    {
        var options = new ExportOptions();

        OptionsValidation.Validate(options).ShouldBeEmpty();
        options.FontSizePoints.ShouldBeGreaterThanOrEqualTo(14);
        options.LineSpacing.ShouldBeGreaterThanOrEqualTo(1.5);
    }

    [Theory]
    [InlineData(nameof(ExportOptions.FontSizePoints), 12)]
    [InlineData(nameof(ExportOptions.LineSpacing), 1.15)]
    [InlineData(nameof(ExportOptions.MarginCentimeters), 0.5)]
    [InlineData(nameof(ExportOptions.TitleFontSizePoints), 10)]
    [InlineData(nameof(ExportOptions.HeadingFontSizePoints), 100)]
    public void Validate_RejectsLayoutBelowTheEasyReadMinimums(string property, double value)
    {
        var options = new ExportOptions();
        typeof(ExportOptions).GetProperty(property)!.SetValue(options, value);

        OptionsValidation.Validate(options).ShouldHaveSingleItem().ShouldStartWith($"Export:{property}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Arial\"/><w:b/>")]
    [InlineData("Times;New")]
    public void Validate_RejectsUnsafeFontNames(string font) =>
        OptionsValidation.Validate(new ExportOptions { DocxFontFamily = font }).ShouldNotBeEmpty();
}
