// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text;
using LecturaFacil.Infrastructure.Observability;
using LecturaFacil.Infrastructure.Tests.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Tests.Observability;

public sealed class SentryEnvelopeForwarderTests
{
    private const string FrontendDsn = "https://clave-publica@sentry.example.com/42";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Forward_WhenTheEnvelopeIsForTheConfiguredDsn_PostsItToThatProjectOnly()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");
        var envelope = Envelope(FrontendDsn);

        var result = await Forwarder(handler).ForwardAsync(envelope, Token);

        result.IsSuccess.ShouldBeTrue();
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.ShouldBe(new Uri("https://sentry.example.com/api/42/envelope/"));
        request.Body.ShouldBe(Encoding.UTF8.GetString(envelope));
    }

    [Theory]
    [InlineData("https://otra-clave@sentry.example.com/42")]
    [InlineData("https://clave-publica@atacante.example.com/42")]
    [InlineData("")]
    public async Task Forward_WhenTheEnvelopeNamesAnotherDsn_RejectsItWithoutCallingAnyone(string dsn)
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");

        var result = await Forwarder(handler).ForwardAsync(Envelope(dsn), Token);

        result.Error.ShouldBe(SentryEnvelopeForwarder.InvalidEnvelope);
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("no es json\n{}")]
    [InlineData("[1,2]\n{}")]
    [InlineData("")]
    public async Task Forward_WhenTheHeaderIsNotAJsonObject_RejectsIt(string body)
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");

        var result = await Forwarder(handler).ForwardAsync(Encoding.UTF8.GetBytes(body), Token);

        result.Error.ShouldBe(SentryEnvelopeForwarder.InvalidEnvelope);
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("{\"type\":\"attachment\",\"length\":5}\nhola!")]
    [InlineData("{\"type\":\"replay_event\"}\n{}")]
    [InlineData("{\"type\":\"event\"}\n{}\n{\"type\":\"profile\"}\n{}")]
    [InlineData("{\"length\":2}\n{}")]
    [InlineData("{\"type\":\"event\",\"length\":999}\n{}")]
    [InlineData("")]
    public async Task Forward_WhenAnItemIsNotAnErrorReport_RejectsTheWholeEnvelope(string items)
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");
        var envelope = Encoding.UTF8.GetBytes($"{{\"dsn\":\"{FrontendDsn}\"}}\n{items}");

        var result = await Forwarder(handler).ForwardAsync(envelope, Token);

        result.Error.ShouldBe(SentryEnvelopeForwarder.InvalidEnvelope);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Forward_AcceptsItemsWithAnExplicitLengthThatContainNewlines()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");
        var envelope = Encoding.UTF8.GetBytes(
            $"{{\"dsn\":\"{FrontendDsn}\"}}\n{{\"type\":\"event\",\"length\":5}}\n{{\n}}\n\n{{\"type\":\"client_report\"}}\n{{}}\n");

        var result = await Forwarder(handler).ForwardAsync(envelope, Token);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Forward_WhenTheEnvelopeHasTooManyItems_RejectsIt()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");
        var items = string.Concat(Enumerable.Repeat("{\"type\":\"event\"}\n{}\n", SentryEnvelopeForwarder.MaxItemsPerEnvelope + 1));

        var result = await Forwarder(handler).ForwardAsync(Encoding.UTF8.GetBytes($"{{\"dsn\":\"{FrontendDsn}\"}}\n{items}"), Token);

        result.Error.ShouldBe(SentryEnvelopeForwarder.InvalidEnvelope);
    }

    [Fact]
    public async Task Forward_WhenTheFrontendDsnIsNotConfigured_IsDisabled()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");

        var forwarder = Forwarder(handler, string.Empty);
        var result = await forwarder.ForwardAsync(Envelope(FrontendDsn), Token);

        forwarder.IsEnabled.ShouldBeFalse();
        result.Error.ShouldBe(SentryEnvelopeForwarder.Disabled);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Forward_WhenSentryRejectsOrIsDown_ReturnsUnavailable()
    {
        var rejecting = FakeHttpMessageHandler.Returning(HttpStatusCode.TooManyRequests, "{}");
        var down = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("sin red"));

        (await Forwarder(rejecting).ForwardAsync(Envelope(FrontendDsn), Token)).Error.ShouldBe(SentryEnvelopeForwarder.Unavailable);
        (await Forwarder(down).ForwardAsync(Envelope(FrontendDsn), Token)).Error.ShouldBe(SentryEnvelopeForwarder.Unavailable);
    }

    private static byte[] Envelope(string dsn) =>
        Encoding.UTF8.GetBytes($"{{\"dsn\":\"{dsn}\",\"sent_at\":\"2026-11-16T10:00:00Z\"}}\n{{\"type\":\"event\"}}\n{{\"message\":\"fallo\"}}");

    private static SentryEnvelopeForwarder Forwarder(HttpMessageHandler handler, string dsn = FrontendDsn) =>
        new(
            new SingleClientFactory(handler),
            Options.Create(new ObservabilityOptions { FrontendSentryDsn = dsn }),
            NullLogger<SentryEnvelopeForwarder>.Instance);

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            name.ShouldBe(SentryEnvelopeForwarder.HttpClientName);
            return new HttpClient(handler, disposeHandler: false);
        }
    }
}
