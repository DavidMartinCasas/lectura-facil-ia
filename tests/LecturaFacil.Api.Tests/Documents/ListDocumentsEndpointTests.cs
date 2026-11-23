// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text.Json;
using LecturaFacil.Api.Tests.Security;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Api.Tests.Documents;

/// <summary><c>GET /api/documents</c>: la lista "Mis documentos", con el repositorio en memoria.</summary>
public sealed class ListDocumentsEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly Uri ListUri = new("/api/documents", UriKind.Relative);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_WhenThereAreNoDocuments_ReturnsAnEmptyPageWithoutNextCursor()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.GetAsync(ListUri, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        json.RootElement.GetProperty("items").GetArrayLength().ShouldBe(0);
        json.RootElement.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task List_ReturnsTitleDateStatusAndProvider_OnlyOfTheCurrentUser_WithoutModelNames()
    {
        await using var factory = new DocumentsApiFactory();
        var received = await SeedAsync(factory, TestUsers.DefaultOwner, "Bases", Now.AddMinutes(-1));
        var adapted = await SeedAsync(factory, TestUsers.DefaultOwner, "Convocatoria", Now, Adapt);
        await SeedAsync(factory, OwnerId.From(Guid.NewGuid()), "Ajeno", Now.AddMinutes(1));
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.GetAsync(ListUri, Token);

        var body = await response.Content.ReadAsStringAsync(Token);
        using var json = JsonDocument.Parse(body);
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBe(2);
        items[0].GetProperty("id").GetGuid().ShouldBe(adapted.Id.Value);
        items[0].GetProperty("title").GetString().ShouldBe("Convocatoria");
        items[0].GetProperty("status").GetString().ShouldBe("Draft");
        items[0].GetProperty("createdAt").GetDateTimeOffset().ShouldBe(Now);
        items[0].GetProperty("provider").GetString().ShouldBe("Servidor local");
        items[1].GetProperty("id").GetGuid().ShouldBe(received.Id.Value);
        items[0].GetProperty("warningCount").GetInt32().ShouldBeGreaterThanOrEqualTo(0);
        items[0].GetProperty("readability").GetProperty("adapted").GetProperty("value").ValueKind.ShouldBe(JsonValueKind.Number);
        items[1].GetProperty("provider").ValueKind.ShouldBe(JsonValueKind.Null);
        items[1].GetProperty("warningCount").ValueKind.ShouldBe(JsonValueKind.Null);
        items[1].GetProperty("readability").ValueKind.ShouldBe(JsonValueKind.Null);
        body.ShouldNotContain("Ajeno");
        body.ShouldNotContain("modelo-secreto");
        body.ShouldNotContain("Texto de prueba");
    }

    [Fact]
    public async Task List_PagesWithTheNextCursor_UntilThereAreNoMoreDocuments()
    {
        await using var factory = new DocumentsApiFactory();
        var third = await SeedAsync(factory, TestUsers.DefaultOwner, "Uno", Now.AddMinutes(-2));
        var second = await SeedAsync(factory, TestUsers.DefaultOwner, "Dos", Now.AddMinutes(-1));
        var first = await SeedAsync(factory, TestUsers.DefaultOwner, "Tres", Now);
        using var client = await factory.CreateApiClientAsync();

        var (firstIds, cursor) = await GetPageAsync(client, "/api/documents?limit=2");
        var (secondIds, lastCursor) = await GetPageAsync(client, $"/api/documents?limit=2&cursor={Uri.EscapeDataString(cursor!)}");

        firstIds.ShouldBe([first.Id.Value, second.Id.Value]);
        secondIds.ShouldBe([third.Id.Value]);
        lastCursor.ShouldBeNull();
    }

    [Fact]
    public async Task List_FiltersByTitleAndRepeatedStatus()
    {
        await using var factory = new DocumentsApiFactory();
        var adaptedBases = await SeedAsync(factory, TestUsers.DefaultOwner, "Bases de ayudas", Now, Adapt);
        await SeedAsync(factory, TestUsers.DefaultOwner, "Bases de empleo", Now.AddMinutes(1));
        await SeedAsync(factory, TestUsers.DefaultOwner, "Convocatoria", Now.AddMinutes(2), Adapt);
        using var client = await factory.CreateApiClientAsync();

        var (ids, _) = await GetPageAsync(client, "/api/documents?title=BASES&status=Draft&status=Validated");

        ids.ShouldBe([adaptedBases.Id.Value]);
    }

    [Theory]
    [InlineData("/api/documents?status=Archivado", "documents.invalid_status")]
    [InlineData("/api/documents?cursor=no-vale", "documents.invalid_cursor")]
    [InlineData("/api/documents?limit=0", "documents.invalid_page_size")]
    [InlineData("/api/documents?limit=1000", "documents.invalid_page_size")]
    public async Task List_WhenTheQueryIsNotValid_Returns400WithTheErrorCode(string uri, string code)
    {
        await using var factory = new DocumentsApiFactory();
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.GetAsync(new Uri(uri, UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        json.RootElement.GetProperty("code").GetString().ShouldBe(code);
    }

    private static async Task<(List<Guid> Ids, string? NextCursor)> GetPageAsync(HttpClient client, string uri)
    {
        using var response = await client.GetAsync(new Uri(uri, UriKind.Relative), Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        var ids = json.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();
        return (ids, json.RootElement.GetProperty("nextCursor").GetString());
    }

    private static void Adapt(Document document)
    {
        document.StartProcessing(Now);
        document.CompleteAdaptation(
            AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Quién?", ["Una frase."])], [])!,
            new AdaptationMetadata("Servidor local", "modelo-secreto", "adapter.v1", 1, 1, TimeSpan.FromSeconds(1)),
            GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10),
            MeaningVerification.NotVerified("Texto de prueba.", 10),
            Now);
    }

    private static async Task<Document> SeedAsync(
        DocumentsApiFactory factory,
        OwnerId owner,
        string title,
        DateTimeOffset createdAt,
        Action<Document>? prepare = null)
    {
        var document = Document.Submit(
            owner,
            DocumentTitle.Create(title),
            OriginalText.Create("Texto de prueba suficientemente largo.", new TextLengthLimits(10, 1_000)),
            createdAt);
        prepare?.Invoke(document);
        await factory.Repository.AddAsync(document, Token);
        return document;
    }
}
