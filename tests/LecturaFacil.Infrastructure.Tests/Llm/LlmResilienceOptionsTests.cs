// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Llm;

namespace LecturaFacil.Infrastructure.Tests.Llm;

public sealed class LlmResilienceOptionsTests
{
    [Fact]
    public void Validate_WithTheDefaults_IsValid()
    {
        OptionsValidation.Validate(new LlmResilienceOptions()).ShouldBeEmpty();
    }

    [Fact]
    public void Defaults_UseAShortRetryAndTheHealthCheck()
    {
        var options = new LlmResilienceOptions();

        options.MaxRetryAttempts.ShouldBe(1);
        options.HealthCheckEnabled.ShouldBeTrue();
    }

    [Theory]
    [InlineData(nameof(LlmResilienceOptions.MaxRetryAttempts), 4)]
    [InlineData(nameof(LlmResilienceOptions.RetryDelayMilliseconds), 50)]
    [InlineData(nameof(LlmResilienceOptions.FailureRatio), 0)]
    [InlineData(nameof(LlmResilienceOptions.FailureRatio), 1.5)]
    [InlineData(nameof(LlmResilienceOptions.MinimumThroughput), 1)]
    [InlineData(nameof(LlmResilienceOptions.SamplingDurationSeconds), 0)]
    [InlineData(nameof(LlmResilienceOptions.BreakDurationSeconds), 0)]
    [InlineData(nameof(LlmResilienceOptions.HealthCheckIntervalSeconds), 1)]
    public void Validate_WhenAValueIsOutOfRange_NamesTheOption(string property, double value)
    {
        var options = new LlmResilienceOptions();
        var info = typeof(LlmResilienceOptions).GetProperty(property)!;
        info.SetValue(options, Convert.ChangeType(value, info.PropertyType, System.Globalization.CultureInfo.InvariantCulture));

        OptionsValidation.Validate(options)
            .ShouldContain(error => error.Contains($"Llm:Resilience:{property}", StringComparison.Ordinal));
    }
}
