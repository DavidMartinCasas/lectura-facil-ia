// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Tests.Common;

public sealed class LimitsOptionsTests
{
    [Fact]
    public void Validate_WithDefaults_IsValid()
    {
        Validate(new LimitsOptions()).ShouldBeEmpty();
    }

    [Fact]
    public void Defaults_WhenNothingIsConfigured_AreConservative()
    {
        var options = new LimitsOptions();

        options.MaxFileSizeBytes.ShouldBe(5 * 1024 * 1024);
        options.MaxCharacters.ShouldBe(20_000);
        options.MaxWordsPerSentence.ShouldBe(15);
        options.MaxConnectorsPerSentence.ShouldBe(1);
        options.MaxFindingsPerDocument.ShouldBe(500);
        options.MaxOutputTokens.ShouldBe(4_096);
        options.LlmTimeoutSeconds.ShouldBe(120);
        options.MinCharacters.ShouldBe(50);
        options.MaxPdfPages.ShouldBe(100);
        options.PdfExtractionTimeoutSeconds.ShouldBe(15);
        options.MaxConcurrentPdfExtractions.ShouldBe(2);
    }

    [Fact]
    public void TextLengthLimits_WhenCalled_UsesMinAndMaxCharacters()
    {
        var limits = new LimitsOptions { MinCharacters = 20, MaxCharacters = 300 }.TextLengthLimits;

        limits.Minimum.ShouldBe(20);
        limits.Maximum.ShouldBe(300);
    }

    [Theory]
    [InlineData(nameof(LimitsOptions.MaxFileSizeBytes), 0)]
    [InlineData(nameof(LimitsOptions.MaxFileSizeBytes), 100 * 1024 * 1024)]
    [InlineData(nameof(LimitsOptions.MaxCharacters), 10)]
    [InlineData(nameof(LimitsOptions.MaxCharacters), 1_000_000)]
    [InlineData(nameof(LimitsOptions.MaxWordsPerSentence), 0)]
    [InlineData(nameof(LimitsOptions.MaxWordsPerSentence), 100)]
    [InlineData(nameof(LimitsOptions.MaxConnectorsPerSentence), 0)]
    [InlineData(nameof(LimitsOptions.MaxConnectorsPerSentence), 11)]
    [InlineData(nameof(LimitsOptions.MaxFindingsPerDocument), 9)]
    [InlineData(nameof(LimitsOptions.MaxFindingsPerDocument), 5_001)]
    [InlineData(nameof(LimitsOptions.MaxOutputTokens), 10)]
    [InlineData(nameof(LimitsOptions.MaxOutputTokens), 1_000_000)]
    [InlineData(nameof(LimitsOptions.LlmTimeoutSeconds), 0)]
    [InlineData(nameof(LimitsOptions.LlmTimeoutSeconds), 3_600)]
    [InlineData(nameof(LimitsOptions.MinCharacters), 0)]
    [InlineData(nameof(LimitsOptions.MinCharacters), 101)]
    [InlineData(nameof(LimitsOptions.MaxPdfPages), 0)]
    [InlineData(nameof(LimitsOptions.MaxPdfPages), 5_000)]
    [InlineData(nameof(LimitsOptions.PdfExtractionTimeoutSeconds), 0)]
    [InlineData(nameof(LimitsOptions.PdfExtractionTimeoutSeconds), 600)]
    [InlineData(nameof(LimitsOptions.MaxConcurrentPdfExtractions), 0)]
    [InlineData(nameof(LimitsOptions.MaxConcurrentPdfExtractions), 100)]
    public void Validate_WhenValueIsOutOfRange_ReportsTheOption(string property, int value)
    {
        var options = new LimitsOptions();
        typeof(LimitsOptions).GetProperty(property)!.SetValue(options, value);

        var errors = Validate(options);

        errors.ShouldHaveSingleItem().ShouldContain($"{LimitsOptions.SectionName}:{property}");
    }

    private static List<string> Validate(LimitsOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return [.. results.Select(result => result.ErrorMessage ?? string.Empty)];
    }
}
