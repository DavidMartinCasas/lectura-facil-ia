// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.HumanVerification;

namespace LecturaFacil.Infrastructure.Tests.HumanVerification;

public sealed class TurnstileOptionsTests
{
    [Fact]
    public void Defaults_AreDisabledAndValidWithoutKeys()
    {
        var options = new TurnstileOptions();

        options.Enabled.ShouldBeFalse();
        options.TimeoutSeconds.ShouldBe(5);
        OptionsValidation.Validate(options).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "secreta", "Turnstile:SiteKey")]
    [InlineData("publica", "", "Turnstile:SecretKey")]
    public void Validate_WhenEnabledWithoutAKey_Fails(string siteKey, string secretKey, string missing)
    {
        var options = new TurnstileOptions { Enabled = true, SiteKey = siteKey, SecretKey = secretKey };

        OptionsValidation.Validate(options).ShouldContain(message => message.Contains(missing, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenEnabledWithBothKeys_Succeeds()
    {
        var options = new TurnstileOptions { Enabled = true, SiteKey = "publica", SecretKey = "secreta" };

        OptionsValidation.Validate(options).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Validate_WhenTheTimeoutIsOutOfRange_Fails(int seconds)
    {
        var options = new TurnstileOptions { TimeoutSeconds = seconds };

        OptionsValidation.Validate(options).ShouldContain(message => message.Contains("Turnstile:TimeoutSeconds", StringComparison.Ordinal));
    }
}
