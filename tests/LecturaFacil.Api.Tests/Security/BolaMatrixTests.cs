// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using LecturaFacil.Api.Security;
using LecturaFacil.Api.Tests.Documents;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Tests.Security;

/// <summary>
/// Matriz de autorización de todos los endpoints de documentos (US-15): el usuario A no llega a nada del usuario B (404,
/// igual que si no existiera; OWASP API1 · BOLA), sin sesión todo responde 401 y sin token antiforgery nada cambia de estado.
/// </summary>
public sealed class BolaMatrixTests
{
    /// <summary>Cada endpoint que recibe el id de un documento, con un cuerpo válido para que lo que decida sea la propiedad.</summary>
    public static TheoryData<string, string> DocumentEndpoints => new()
    {
        { "GET", "/api/documents/{id}" },
        { "GET", "/api/documents/{id}/events" },
        { "PUT", "/api/documents/{id}/versions/1" },
        { "POST", "/api/documents/{id}/key-facts/1/acknowledge" },
        { "POST", "/api/documents/{id}/key-facts/acknowledge" },
        { "POST", "/api/documents/{id}/export?format=txt" },
        { "POST", "/api/documents/{id}/retry" },
        { "POST", "/api/documents/{id}/validation/send" },
        { "POST", "/api/documents/{id}/validation" },
        { "POST", "/api/documents/{id}/validation/withdraw" },
        { "POST", "/api/documents/{id}/publish" },
    };

    /// <summary>Todos los endpoints de documentos, también los que no reciben un id.</summary>
    public static TheoryData<string, string> AllDocumentEndpoints
    {
        get
        {
            var all = DocumentEndpoints;
            all.Add("GET", "/api/documents");
            all.Add("POST", "/api/documents");
            return all;
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(DocumentEndpoints))]
    public async Task DocumentEndpoint_WhenTheDocumentIsOfAnotherUser_Returns404LikeAMissingOne(string method, string template)
    {
        await using var factory = new DocumentsApiFactory();
        var documentOfB = GetDocumentEndpointTests.Seed(factory, TestUsers.OtherOwner, GetDocumentEndpointTests.Adapt);
        using var clientOfA = await factory.CreateApiClientAsync(TestUsers.Default);

        using var foreign = await clientOfA.SendAsync(Request(method, template, documentOfB.Id.Value), Token);
        using var missing = await clientOfA.SendAsync(Request(method, template, Guid.NewGuid()), Token);

        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        GetDocumentEndpointTests.WithoutTraceId(await foreign.Content.ReadAsStringAsync(Token))
            .ShouldBe(GetDocumentEndpointTests.WithoutTraceId(await missing.Content.ReadAsStringAsync(Token)));
        factory.Repository.Added.Single().AdaptedVersions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task List_ReturnsOnlyTheDocumentsOfTheUserWithTheSession()
    {
        await using var factory = new DocumentsApiFactory();
        var documentOfA = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner);
        GetDocumentEndpointTests.Seed(factory, TestUsers.OtherOwner);
        using var clientOfA = await factory.CreateApiClientAsync(TestUsers.Default);

        var page = await clientOfA.GetFromJsonAsync<ListedPage>(new Uri("/api/documents", UriKind.Relative), Token);

        page.ShouldNotBeNull().Items.Select(document => document.Id).ShouldBe([documentOfA.Id.Value]);
    }

    [Theory]
    [MemberData(nameof(AllDocumentEndpoints))]
    public async Task DocumentEndpoint_WithoutASession_Returns401(string method, string template)
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var anonymous = await factory.CreateAnonymousClientAsync();

        using var response = await anonymous.SendAsync(Request(method, template, document.Id.Value), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        factory.Repository.Added.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("/api/status/llm")]
    [InlineData("/api/auth/me")]
    public async Task OtherApiEndpoints_WithoutASession_Return401(string path)
    {
        await using var factory = new DocumentsApiFactory();
        using var anonymous = await factory.CreateAnonymousClientAsync();

        using var response = await anonymous.GetAsync(new Uri(path, UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("PUT", "/api/documents/{id}/versions/1")]
    [InlineData("POST", "/api/documents/{id}/key-facts/1/acknowledge")]
    [InlineData("POST", "/api/documents/{id}/key-facts/acknowledge")]
    [InlineData("POST", "/api/documents/{id}/export?format=txt")]
    [InlineData("POST", "/api/documents/{id}/retry")]
    [InlineData("POST", "/api/documents/{id}/validation/send")]
    [InlineData("POST", "/api/documents/{id}/validation")]
    [InlineData("POST", "/api/documents/{id}/validation/withdraw")]
    [InlineData("POST", "/api/documents/{id}/publish")]
    [InlineData("POST", "/api/documents")]
    [InlineData("POST", "/api/auth/logout")]
    public async Task StateChangingRequest_WithoutTheAntiforgeryToken_Returns400(string method, string template)
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();
        client.DefaultRequestHeaders.Remove(AntiforgeryMiddleware.HeaderName);

        using var response = await client.SendAsync(Request(method, template, document.Id.Value), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain(AntiforgeryMiddleware.InvalidTokenCode);
        document.AdaptedVersions.Count.ShouldBe(1);
        factory.Repository.Added.Count.ShouldBe(1);
    }

    [Fact]
    public async Task StateChangingRequest_WithTheTokenOfAnotherUser_Returns400()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var clientOfB = await factory.CreateApiClientAsync(TestUsers.Other);
        using var clientOfA = await factory.CreateApiClientAsync(TestUsers.Default);
        clientOfA.DefaultRequestHeaders.Remove(AntiforgeryMiddleware.HeaderName);
        clientOfA.DefaultRequestHeaders.Add(
            AntiforgeryMiddleware.HeaderName,
            clientOfB.DefaultRequestHeaders.GetValues(AntiforgeryMiddleware.HeaderName));

        using var response = await clientOfA.SendAsync(Request("PUT", "/api/documents/{id}/versions/1", document.Id.Value), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        document.AdaptedVersions.Count.ShouldBe(1);
    }

    private static HttpRequestMessage Request(string method, string template, Guid id)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), new Uri(template.Replace("{id}", id.ToString(), StringComparison.Ordinal), UriKind.Relative));
        request.Content = template switch
        {
            _ when method == "GET" => null,
            _ when template.EndsWith("/versions/1", StringComparison.Ordinal) => JsonContent.Create(ValidEdit),
            _ when template.EndsWith("/acknowledge", StringComparison.Ordinal) => JsonContent.Create(new { version = 1 }),
            _ when template.EndsWith("/validation/send", StringComparison.Ordinal) => JsonContent.Create(new { version = 1 }),
            _ when template.EndsWith("/validation", StringComparison.Ordinal) =>
                JsonContent.Create(new { outcome = "Approved", validatedBy = "Grupo de lectura" }),
            "/api/documents" => JsonContent.Create(new { text = new string('a', 100) }),
            _ => null,
        };
        return request;
    }

    private static readonly object ValidEdit = new
    {
        title = "Ayudas para el alquiler",
        sections = new[] { new { question = "¿Quién puede pedirla?", sentences = new[] { "Una frase." } } },
        glossary = Array.Empty<object>(),
    };

    private sealed record ListedDocument(Guid Id);
    private sealed record ListedPage(List<ListedDocument> Items, string? NextCursor);
}
