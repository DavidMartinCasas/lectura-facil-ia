// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Api.Observability;
using Sentry;

namespace LecturaFacil.Api.Tests.Observability;

public sealed class SentryEventScrubberTests
{
    private const string DocumentText = "La Concejalía de Vivienda convoca ayudas para el alquiler.";

    [Fact]
    public void Scrub_RemovesTheRequestBodyCookiesQueryAndSensitiveHeaders()
    {
        var sentryEvent = new SentryEvent();
        sentryEvent.Request.Data = new { text = DocumentText };
        sentryEvent.Request.Cookies = "sesion=abc";
        sentryEvent.Request.QueryString = "?email=ana@example.com";
        sentryEvent.Request.Url = "https://app.example.com/api/documents?text=hola";
        sentryEvent.Request.Headers["Authorization"] = "Bearer secreto";
        sentryEvent.Request.Headers["Cookie"] = "sesion=abc";
        sentryEvent.Request.Headers["X-Api-Key"] = "clave";
        sentryEvent.Request.Headers["User-Agent"] = "Navegador";
        sentryEvent.Request.Env["REMOTE_ADDR"] = "203.0.113.7";

        var scrubbed = SentryEventScrubber.Scrub(sentryEvent);

        scrubbed.Request.Data.ShouldBeNull();
        scrubbed.Request.Cookies.ShouldBeNull();
        scrubbed.Request.QueryString.ShouldBeNull();
        scrubbed.Request.Url.ShouldBe("https://app.example.com/api/documents");
        scrubbed.Request.Headers.Keys.ShouldBe(["User-Agent"]);
        scrubbed.Request.Env.ShouldBeEmpty();
    }

    [Fact]
    public void Scrub_RemovesTheUserAndTheServerName()
    {
        var sentryEvent = new SentryEvent
        {
            User = new SentryUser { Email = "ana@example.com", IpAddress = "203.0.113.7", Username = "ana" },
            ServerName = "servidor-interno",
        };

        var scrubbed = SentryEventScrubber.Scrub(sentryEvent);

        scrubbed.User.Email.ShouldBeNull();
        scrubbed.User.IpAddress.ShouldBeNull();
        scrubbed.User.Username.ShouldBeNull();
        scrubbed.ServerName.ShouldBeNull();
    }

    [Theory]
    [InlineData("text")]
    [InlineData("content")]
    [InlineData("original")]
    [InlineData("adapted")]
    [InlineData("originalText")]
    [InlineData("AdaptedContent")]
    public void Scrub_RedactsExtrasTagsAndContextsNamedLikeDocumentText(string field)
    {
        var sentryEvent = new SentryEvent();
        sentryEvent.SetExtra(field, DocumentText);
        sentryEvent.SetExtra("documentId", "8c1f");
        sentryEvent.SetTag(field, DocumentText);
        sentryEvent.Contexts[field] = DocumentText;

        var scrubbed = SentryEventScrubber.Scrub(sentryEvent);

        scrubbed.Extra[field].ShouldBe(SentryEventScrubber.Redacted);
        scrubbed.Extra["documentId"].ShouldBe("8c1f");
        scrubbed.Tags.ShouldNotContainKey(field);
        scrubbed.Contexts.ContainsKey(field).ShouldBeFalse();
    }

    [Fact]
    public void Scrub_RedactsBreadcrumbDataNamedLikeDocumentText()
    {
        var breadcrumb = new Breadcrumb(
            "Petición",
            "http",
            new Dictionary<string, string> { ["text"] = DocumentText, ["status_code"] = "500" },
            "http",
            BreadcrumbLevel.Info);

        var scrubbed = SentryEventScrubber.Scrub(breadcrumb);

        scrubbed.Data!["text"].ShouldBe(SentryEventScrubber.Redacted);
        scrubbed.Data["status_code"].ShouldBe("500");
        scrubbed.Message.ShouldBe("Petición");
    }

    [Fact]
    public void Scrub_LeavesBreadcrumbsWithoutSensitiveDataUntouched()
    {
        var breadcrumb = new Breadcrumb("Adaptación terminada", "default");

        SentryEventScrubber.Scrub(breadcrumb).ShouldBeSameAs(breadcrumb);
    }
}
