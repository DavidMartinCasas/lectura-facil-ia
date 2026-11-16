// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Observability;

namespace LecturaFacil.Infrastructure.Tests.Observability;

public sealed class ObservabilityOptionsTests
{
    [Fact]
    public void Defaults_AreValidAndLeaveSentryDisabled()
    {
        var options = new ObservabilityOptions();

        OptionsValidation.Validate(options).ShouldBeEmpty();
        options.SentryDsn.ShouldBeEmpty();
        options.FrontendSentryDsn.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("https://clave@sentry.example.com/1", "https://sentry.example.com/api/1/envelope/")]
    [InlineData("https://clave@sentry.example.com:8443/ruta/7", "https://sentry.example.com:8443/ruta/api/7/envelope/")]
    [InlineData("http://clave@localhost:9000/3", "http://localhost:9000/api/3/envelope/")]
    public void SentryEndpoint_WithAValidDsn_IsTheEnvelopeUrlOfItsProject(string dsn, string expected) =>
        SentryEndpoint.TryParse(dsn).ShouldBe(new Uri(expected));

    [Theory]
    [InlineData("no es una url")]
    [InlineData("https://sentry.example.com/1")]
    [InlineData("http://clave@sentry.example.com/1")]
    [InlineData("https://clave@sentry.example.com/")]
    [InlineData("https://clave@sentry.example.com/proyecto")]
    [InlineData("https://clave@sentry.example.com/1?x=1")]
    [InlineData("ftp://clave@sentry.example.com/1")]
    public void Validate_WhenADsnIsNotValid_FailsWithoutShowingIt(string dsn)
    {
        var errors = OptionsValidation.Validate(new ObservabilityOptions { SentryDsn = dsn, FrontendSentryDsn = dsn });

        errors.Count.ShouldBe(2);
        errors.ShouldAllBe(error => !error.Contains(dsn, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(nameof(ObservabilityOptions.Environment), "con espacios")]
    [InlineData(nameof(ObservabilityOptions.Release), "<script>")]
    public void Validate_WhenANameHasUnexpectedCharacters_Fails(string property, string value)
    {
        var options = new ObservabilityOptions();
        typeof(ObservabilityOptions).GetProperty(property)!.SetValue(options, value);

        OptionsValidation.Validate(options).ShouldHaveSingleItem().ShouldContain($"Observability:{property}");
    }
}
