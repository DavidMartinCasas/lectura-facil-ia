// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Llm;

namespace LecturaFacil.Infrastructure.Tests.Llm;

public sealed class LlmOptionsTests
{
    private const string SecretValue = "secreto-secreto-secreto";

    [Fact]
    public void Validate_WhenPrimaryIsComplete_IsValid()
    {
        OptionsValidation.Validate(ValidOptions()).ShouldBeEmpty();
    }

    [Fact]
    public void Temperature_ByDefault_IsZero()
    {
        new LlmProviderOptions().Temperature.ShouldBe(0);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    [InlineData(double.NaN)]
    public void Validate_WhenTemperatureIsOutOfRange_ReportsIt(double temperature)
    {
        var options = ValidOptions();
        options.Primary.Temperature = temperature;

        OptionsValidation.Validate(options).ShouldContain(error => error.Contains("Llm:Primary:Temperature", StringComparison.Ordinal));
    }

    [Fact]
    public void DisableThinking_ByDefault_IsFalse()
    {
        new LlmProviderOptions().DisableThinking.ShouldBeFalse();
    }

    [Theory]
    [InlineData(LlmProviderType.OpenAICompatible, false)]
    [InlineData(LlmProviderType.AzureFoundry, true)]
    public void Validate_DisableThinking_IsOnlyAllowedWithOpenAICompatible(LlmProviderType type, bool expectError)
    {
        var options = ValidOptions();
        options.Primary.Type = type;
        options.Primary.DisableThinking = true;

        OptionsValidation.Validate(options)
            .Any(error => error.Contains("Llm:Primary:DisableThinking", StringComparison.Ordinal))
            .ShouldBe(expectError);
    }

    [Fact]
    public void Fallback_ByDefault_IsDisabled()
    {
        new LlmOptions().Fallback.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WhenFallbackIsDisabled_DoesNotRequireItsConfiguration()
    {
        var options = ValidOptions();
        options.Fallback = new FallbackLlmProviderOptions { Enabled = false };

        OptionsValidation.Validate(options).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenFallbackIsEnabledButIncomplete_ReportsItsMissingOptions()
    {
        var options = ValidOptions();
        options.Fallback = new FallbackLlmProviderOptions { Enabled = true };

        var errors = OptionsValidation.Validate(options);

        errors.ShouldContain(error => error.Contains("Llm:Fallback:Type", StringComparison.Ordinal));
        errors.ShouldContain(error => error.Contains("Llm:Fallback:Endpoint", StringComparison.Ordinal));
        errors.ShouldContain(error => error.Contains("Llm:Fallback:ApiKey", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenFallbackIsEnabledAndComplete_IsValid()
    {
        var options = ValidOptions();
        options.Fallback = new FallbackLlmProviderOptions
        {
            Enabled = true,
            Type = LlmProviderType.AzureFoundry,
            DisplayName = "Nube",
            Endpoint = "https://proveedor.example.com/",
            ApiKey = SecretValue,
            AdapterModel = "modelo-a",
            VerifierModel = "modelo-b",
        };

        OptionsValidation.Validate(options).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(nameof(LlmProviderOptions.DisplayName), "Llm:Primary:DisplayName")]
    [InlineData(nameof(LlmProviderOptions.Endpoint), "Llm:Primary:Endpoint")]
    [InlineData(nameof(LlmProviderOptions.ApiKey), "Llm:Primary:ApiKey")]
    [InlineData(nameof(LlmProviderOptions.AdapterModel), "Llm:Primary:AdapterModel")]
    [InlineData(nameof(LlmProviderOptions.VerifierModel), "Llm:Primary:VerifierModel")]
    public void Validate_WhenPrimaryFieldIsMissing_ReportsWhichOption(string property, string expectedPath)
    {
        var options = ValidOptions();
        typeof(LlmProviderOptions).GetProperty(property)!.SetValue(options.Primary, " ");

        OptionsValidation.Validate(options).ShouldHaveSingleItem().ShouldContain(expectedPath);
    }

    [Fact]
    public void Validate_WhenPrimaryTypeIsMissing_ReportsWhichOption()
    {
        var options = ValidOptions();
        options.Primary.Type = null;

        OptionsValidation.Validate(options).ShouldHaveSingleItem().ShouldContain("Llm:Primary:Type");
    }

    [Fact]
    public void Validate_WhenPrimaryTypeIsNotDefined_ReportsWhichOption()
    {
        var options = ValidOptions();
        options.Primary.Type = (LlmProviderType)999;

        OptionsValidation.Validate(options).ShouldHaveSingleItem().ShouldContain("Llm:Primary:Type");
    }

    [Fact]
    public void Validate_WhenEnabledFallbackTypeIsNotDefined_ReportsWhichOption()
    {
        var options = ValidOptions();
        options.Fallback = new FallbackLlmProviderOptions
        {
            Enabled = true,
            Type = (LlmProviderType)999,
            DisplayName = "Nube",
            Endpoint = "https://proveedor.example.com/",
            ApiKey = SecretValue,
            AdapterModel = "modelo-a",
            VerifierModel = "modelo-b",
        };

        OptionsValidation.Validate(options).ShouldHaveSingleItem().ShouldContain("Llm:Fallback:Type");
    }

    [Fact]
    public void Validate_WhenDisplayNameIsTooLong_ReportsWhichOption()
    {
        var options = ValidOptions();
        options.Primary.DisplayName = new string('a', LlmProviderOptions.MaxDisplayNameLength + 1);

        OptionsValidation.Validate(options).ShouldHaveSingleItem().ShouldContain("Llm:Primary:DisplayName");
    }

    [Theory]
    [InlineData("http://proveedor.example.com/v1")]
    [InlineData("ftp://localhost/")]
    [InlineData("/v1")]
    [InlineData("https://usuario:clave@proveedor.example.com/")]
    public void Validate_WhenEndpointIsNotAllowed_ReportsWhichOptionWithoutItsValue(string endpoint)
    {
        var options = ValidOptions();
        options.Primary.Endpoint = endpoint;

        var error = OptionsValidation.Validate(options).ShouldHaveSingleItem();

        error.ShouldContain("Llm:Primary:Endpoint");
        error.ShouldNotContain(endpoint);
    }

    [Fact]
    public void Validate_WhenAnythingFails_NeverIncludesTheApiKey()
    {
        var options = new LlmOptions
        {
            Primary = new LlmProviderOptions { ApiKey = SecretValue, Endpoint = "nope" },
            Fallback = new FallbackLlmProviderOptions { Enabled = true, ApiKey = SecretValue, Endpoint = "nope" },
        };

        var errors = OptionsValidation.Validate(options);

        errors.ShouldNotBeEmpty();
        errors.ShouldAllBe(error => !error.Contains(SecretValue, StringComparison.Ordinal));
    }

    internal static LlmOptions ValidOptions() => new()
    {
        Primary = new LlmProviderOptions
        {
            Type = LlmProviderType.OpenAICompatible,
            DisplayName = "Servidor propio",
            Endpoint = "http://localhost:11434/v1",
            ApiKey = SecretValue,
            AdapterModel = "modelo-adaptador",
            VerifierModel = "modelo-verificador",
        },
    };
}
