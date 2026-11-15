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
        options.MaxOutputTokens.ShouldBe(4_096);
        options.LlmTimeoutSeconds.ShouldBe(120);
    }

    [Theory]
    [InlineData(nameof(LimitsOptions.MaxFileSizeBytes), 0)]
    [InlineData(nameof(LimitsOptions.MaxFileSizeBytes), 100 * 1024 * 1024)]
    [InlineData(nameof(LimitsOptions.MaxCharacters), 10)]
    [InlineData(nameof(LimitsOptions.MaxCharacters), 1_000_000)]
    [InlineData(nameof(LimitsOptions.MaxWordsPerSentence), 0)]
    [InlineData(nameof(LimitsOptions.MaxWordsPerSentence), 100)]
    [InlineData(nameof(LimitsOptions.MaxOutputTokens), 10)]
    [InlineData(nameof(LimitsOptions.MaxOutputTokens), 1_000_000)]
    [InlineData(nameof(LimitsOptions.LlmTimeoutSeconds), 0)]
    [InlineData(nameof(LimitsOptions.LlmTimeoutSeconds), 3_600)]
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
