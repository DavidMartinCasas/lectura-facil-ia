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
    public async Task List_WhenThereAreNoDocuments_ReturnsAnEmptyArray()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.GetAsync(ListUri, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        json.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        json.RootElement.GetArrayLength().ShouldBe(0);
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
        var items = json.RootElement.EnumerateArray().ToList();
        items.Count.ShouldBe(2);
        items[0].GetProperty("id").GetGuid().ShouldBe(adapted.Id.Value);
        items[0].GetProperty("title").GetString().ShouldBe("Convocatoria");
        items[0].GetProperty("status").GetString().ShouldBe("Draft");
        items[0].GetProperty("createdAt").GetDateTimeOffset().ShouldBe(Now);
        items[0].GetProperty("provider").GetString().ShouldBe("Servidor local");
        items[1].GetProperty("id").GetGuid().ShouldBe(received.Id.Value);
        items[1].GetProperty("provider").ValueKind.ShouldBe(JsonValueKind.Null);
        body.ShouldNotContain("Ajeno");
        body.ShouldNotContain("modelo-secreto");
        body.ShouldNotContain("Texto de prueba");
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
