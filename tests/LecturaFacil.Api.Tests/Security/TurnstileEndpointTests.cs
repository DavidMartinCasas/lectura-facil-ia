// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text;
using LecturaFacil.Api.Security;
using LecturaFacil.Api.Tests.Documents;
using LecturaFacil.Infrastructure.HumanVerification;
using LecturaFacil.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Api.Tests.Security;

/// <summary>
/// Desafío anti-bots en <c>POST /api/documents</c> (US-16). Cloudflare se sustituye por un servidor falso: los tests nunca
/// salen a la red. El modelo es <see cref="FakeLlmProvider"/>.
/// </summary>
public sealed class TurnstileEndpointTests
{
    private const string ValidToken = "token-valido";
    private const string SecretKey = "clave-secreta-de-prueba";
    private const string ValidText = "Texto de prueba suficientemente largo para pasar la validación mínima de caracteres.";

    private static readonly Uri DocumentsUri = new("/api/documents", UriKind.Relative);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(null)]
    [InlineData("token-falso")]
    public async Task Submit_WhenTurnstileIsEnabledAndTheTokenIsMissingOrInvalid_Returns400WithoutCallingAnyLlm(string? token)
    {
        await using var baseFactory = new DocumentsApiFactory();
        var llm = TestServices.ExampleLlm();
        var cloudflare = new FakeCloudflare();
        await using var factory = WithTurnstile(baseFactory, cloudflare, llm);
        using var client = await factory.CreateApiClientAsync();

        using var response = await PostTextAsync(client, token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain("human_verification.failed");
        baseFactory.Repository.Added.ShouldBeEmpty();
        llm.Requests.ShouldBeEmpty();
        cloudflare.Requests.ShouldBe(token is null ? 0 : 1);
    }

    [Fact]
    public async Task Submit_WhenTurnstileIsEnabledAndTheTokenIsValid_AcceptsTheDocument()
    {
        await using var baseFactory = new DocumentsApiFactory();
        var cloudflare = new FakeCloudflare();
        await using var factory = WithTurnstile(baseFactory, cloudflare, TestServices.ExampleLlm());
        using var client = await factory.CreateApiClientAsync();

        using var response = await PostTextAsync(client, ValidToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        baseFactory.Repository.Added.ShouldHaveSingleItem();
        cloudflare.LastBody.ShouldContain("secret=" + SecretKey);
    }

    [Fact]
    public async Task SubmitPdf_WhenTurnstileIsEnabledWithoutToken_Returns400WithoutStoringAnything()
    {
        await using var baseFactory = new DocumentsApiFactory();
        await using var factory = WithTurnstile(baseFactory, new FakeCloudflare(), TestServices.ExampleLlm());
        using var client = await factory.CreateApiClientAsync();
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.7 fichero de prueba"));
        form.Add(file, "file", "prueba.pdf");

        using var response = await client.PostAsync(DocumentsUri, form, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        baseFactory.Repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_WhenTurnstileIsDisabled_AcceptsTheDocumentWithoutAnyToken()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = await factory.CreateApiClientAsync();

        using var response = await PostTextAsync(client, token: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Repository.Added.ShouldHaveSingleItem();
    }

    private static async Task<HttpResponseMessage> PostTextAsync(HttpClient client, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, DocumentsUri) { Content = JsonContent.Create(new { text = ValidText }) };
        if (token is not null)
        {
            request.Headers.Add(HumanVerificationFilter.TokenHeader, token);
        }

        return await client.SendAsync(request, Token);
    }

    private static WebApplicationFactory<Program> WithTurnstile(DocumentsApiFactory factory, FakeCloudflare cloudflare, FakeLlmProvider llm) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Turnstile:Enabled", "true");
            builder.UseSetting("Turnstile:SiteKey", "clave-publica-de-prueba");
            builder.UseSetting("Turnstile:SecretKey", SecretKey);
            builder.ConfigureTestServices(services =>
            {
                services.UseFakeLlm(llm);
                services.AddHttpClient<TurnstileVerifier>().ConfigurePrimaryHttpMessageHandler(() => cloudflare);
            });
        });

    /// <summary>Hace de <c>siteverify</c>: solo acepta <see cref="ValidToken"/>.</summary>
    private sealed class FakeCloudflare : HttpMessageHandler
    {
        private int _requests;

        public int Requests => _requests;

        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            request.RequestUri.ShouldBe(TurnstileVerifier.SiteVerifyUri);
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var success = LastBody.Contains("response=" + ValidToken, StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"success":{{(success ? "true" : "false")}}}""", Encoding.UTF8, "application/json"),
            };
        }
    }
}
