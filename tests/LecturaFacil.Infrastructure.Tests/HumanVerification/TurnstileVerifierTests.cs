// SPDX-License-Identifier: Apache-2.0
using System.Net;
using LecturaFacil.Application.Security;
using LecturaFacil.Infrastructure.HumanVerification;
using LecturaFacil.Infrastructure.Tests.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Tests.HumanVerification;

public sealed class TurnstileVerifierTests
{
    private const string SecretKey = "clave-secreta-de-prueba";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Verify_WhenCloudflareAcceptsTheToken_Succeeds()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, """{"success":true,"error-codes":[]}""");

        var result = await Verifier(handler).VerifyAsync("token-del-widget", Token);

        result.IsSuccess.ShouldBeTrue();
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.ShouldBe(TurnstileVerifier.SiteVerifyUri);
        request.Body.ShouldContain("secret=" + SecretKey);
        request.Body.ShouldContain("response=token-del-widget");
    }

    [Theory]
    [InlineData("""{"success":false,"error-codes":["invalid-input-response"]}""")]
    [InlineData("""{"error-codes":[]}""")]
    [InlineData("no es json")]
    [InlineData("[]")]
    public async Task Verify_WhenCloudflareRejectsTheTokenOrAnswersNonsense_Fails(string body)
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, body);

        var result = await Verifier(handler).VerifyAsync("token-falso", Token);

        result.Error.ShouldBe(HumanVerificationErrors.Failed);
    }

    [Fact]
    public async Task Verify_WhenCloudflareAnswersWithAnError_Fails()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.InternalServerError, """{"success":true}""");

        var result = await Verifier(handler).VerifyAsync("token-del-widget", Token);

        result.Error.ShouldBe(HumanVerificationErrors.Failed);
    }

    [Fact]
    public async Task Verify_WhenCloudflareTimesOut_FailsClosed()
    {
        var handler = new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromMilliseconds(50) };

        var result = await Verifier(client).VerifyAsync("token-del-widget", Token);

        result.Error.ShouldBe(HumanVerificationErrors.Failed);
    }

    [Fact]
    public async Task Verify_WhenTheNetworkFails_FailsClosed()
    {
        var handler = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("sin red"));

        var result = await Verifier(handler).VerifyAsync("token-del-widget", Token);

        result.Error.ShouldBe(HumanVerificationErrors.Failed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Verify_WithoutAToken_FailsWithoutCallingCloudflare(string? token)
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, """{"success":true}""");

        var result = await Verifier(handler).VerifyAsync(token, Token);

        result.Error.ShouldBe(HumanVerificationErrors.Failed);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Verify_WithATokenLongerThanCloudflareAllows_FailsWithoutCallingCloudflare()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, """{"success":true}""");

        var result = await Verifier(handler).VerifyAsync(new string('a', TurnstileVerifier.MaxTokenLength + 1), Token);

        result.Error.ShouldBe(HumanVerificationErrors.Failed);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void IsEnabled_IsAlwaysTrue()
    {
        Verifier(FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "{}")).IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task DisabledVerification_AcceptsAnyRequestWithoutAToken()
    {
        var verification = new DisabledHumanVerification();

        verification.IsEnabled.ShouldBeFalse();
        (await verification.VerifyAsync(null, Token)).IsSuccess.ShouldBeTrue();
    }

    private static TurnstileVerifier Verifier(HttpMessageHandler handler) =>
        Verifier(new HttpClient(handler, disposeHandler: false));

    private static TurnstileVerifier Verifier(HttpClient client) =>
        new(client, Options.Create(new TurnstileOptions { Enabled = true, SiteKey = "clave-publica", SecretKey = SecretKey }), NullLogger<TurnstileVerifier>.Instance);
}
