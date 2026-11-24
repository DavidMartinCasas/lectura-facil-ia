// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LecturaFacil.Api.Tests.Security;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Readability;

namespace LecturaFacil.Api.Tests.Documents;

/// <summary>Edición manual y marca de datos clave revisados (US-13), con el repositorio en memoria.</summary>
public sealed class DocumentReviewEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Put_WhenTheDraftIsEdited_ReturnsANewVersionWithItsGuidelinesAndReadabilityRecalculated()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PutAsJsonAsync(VersionUri(document.Id.Value, 1), Edit("Quien vive de alquiler.", "La ayuda paga una parte del alquiler."), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("status").GetString().ShouldBe("Draft");
        var adaptation = json.RootElement.GetProperty("adaptation");
        adaptation.GetProperty("version").GetInt32().ShouldBe(2);
        adaptation.GetProperty("promptVersion").GetString().ShouldBe("manual");
        adaptation.GetProperty("provider").GetString().ShouldBe("Servidor interno");
        adaptation.GetProperty("pendingHumanValidation").GetBoolean().ShouldBeTrue();
        adaptation.GetProperty("sections")[0].GetProperty("sentences")[1].GetString().ShouldBe("La ayuda paga una parte del alquiler.");
        var percentages = adaptation.GetProperty("guidelines").GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == GuidelineCodes.Percentages);
        percentages.GetProperty("status").GetString().ShouldBe("Compliant");
        json.RootElement.GetProperty("readability").GetProperty("adapted").GetProperty("value").GetDouble().ShouldBe(
            SzigrisztPazosCalculator.Calculate(document.LatestAdaptedVersion!.PlainText).Value);
        document.AdaptedVersions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Put_WhenTheTextLooksLikeHtml_KeepsItAsLiteralText()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PutAsJsonAsync(VersionUri(document.Id.Value, 1), Edit("<script>alert(1)</script>"), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("adaptation").GetProperty("sections")[0].GetProperty("sentences")[0].GetString()
            .ShouldBe("<script>alert(1)</script>");
    }

    [Fact]
    public async Task Put_WhenTheDocumentBelongsToAnotherUser_Returns404LikeAMissingOne()
    {
        await using var factory = new DocumentsApiFactory();
        var foreign = GetDocumentEndpointTests.Seed(factory, OwnerId.From(Guid.NewGuid()), GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        using var foreignResponse = await client.PutAsJsonAsync(VersionUri(foreign.Id.Value, 1), Edit("Otra frase."), Token);
        using var missingResponse = await client.PutAsJsonAsync(VersionUri(Guid.NewGuid(), 1), Edit("Otra frase."), Token);

        foreignResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        GetDocumentEndpointTests.WithoutTraceId(await foreignResponse.Content.ReadAsStringAsync(Token))
            .ShouldBe(GetDocumentEndpointTests.WithoutTraceId(await missingResponse.Content.ReadAsStringAsync(Token)));
        foreign.AdaptedVersions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Put_WhenTheVersionIsNotTheLatest_Returns409()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();
        using var first = await client.PutAsJsonAsync(VersionUri(document.Id.Value, 1), Edit("Primera edición."), Token);

        using var response = await client.PutAsJsonAsync(VersionUri(document.Id.Value, 1), Edit("Segunda edición."), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("code").GetString().ShouldBe("review.version_conflict");
        document.AdaptedVersions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Put_WhenTheDocumentIsStillBeingAdapted_Returns409()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, seeded => seeded.StartProcessing(DateTimeOffset.UtcNow));
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PutAsJsonAsync(VersionUri(document.Id.Value, 1), Edit("Una frase."), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("{\"title\":\"Ayudas\",\"sections\":[]}")]
    [InlineData("{\"title\":null,\"sections\":[{\"question\":\"¿Qué?\",\"sentences\":[\"Una.\"]}]}")]
    [InlineData("{\"title\":\"Ayudas\",\"sections\":[{\"question\":\"¿Qué?\",\"sentences\":[]}]}")]
    [InlineData("{\"title\":\"Ayudas\",\"sections\":[{\"question\":\"¿Qué?\",\"sentences\":[\"Una.\"]}],\"glossary\":[{\"term\":\"BOP\"}]}")]
    [InlineData("{\"title\":\"Ayudas\",\"sections\":[{\"question\":\" \",\"sentences\":[\"Una.\"]}]}")]
    public async Task Put_WhenTheEditIsNotComplete_Returns400WithoutInternalDetails(string body)
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PutAsync(VersionUri(document.Id.Value, 1), content, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadAsStringAsync(Token);
        problem.ShouldNotContain("Exception");
        problem.ShouldNotContain("LecturaFacil.");
        document.AdaptedVersions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Put_WhenTheEditIsTooLong_Returns400()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PutAsJsonAsync(VersionUri(document.Id.Value, 1), Edit(new string('a', 200_001)), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        document.AdaptedVersions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Acknowledge_MarksTheWarningAsReviewed_AndAllowsExportWhenNoneIsLeft()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        using var first = await client.PostAsJsonAsync(AcknowledgeUri(document.Id.Value, 1), new { version = 1 }, Token);
        using var second = await client.PostAsJsonAsync(AcknowledgeUri(document.Id.Value, 2), new { version = 1 }, Token);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (var json = await ReadJsonAsync(first))
        {
            var adaptation = json.RootElement.GetProperty("adaptation");
            adaptation.GetProperty("canExport").GetBoolean().ShouldBeFalse();
            var fact = adaptation.GetProperty("verification").GetProperty("keyFacts")[1];
            fact.GetProperty("acknowledged").GetBoolean().ShouldBeTrue();
            fact.GetProperty("status").GetString().ShouldBe("Review");
            fact.GetProperty("reasonMessage").GetString().ShouldBe("Conversión calculada; debe confirmarla una persona");
        }

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var last = await ReadJsonAsync(second);
        last.RootElement.GetProperty("adaptation").GetProperty("canExport").GetBoolean().ShouldBeTrue();
        last.RootElement.GetProperty("adaptation").GetProperty("version").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task AcknowledgeAll_MarksEveryWarningAsReviewed_AndAllowsExport()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsJsonAsync(AcknowledgeAllUri(document.Id.Value), new { version = 1 }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        var adaptation = json.RootElement.GetProperty("adaptation");
        adaptation.GetProperty("canExport").GetBoolean().ShouldBeTrue();
        adaptation.GetProperty("pendingKeyFactCount").GetInt32().ShouldBe(0);
        adaptation.GetProperty("version").GetInt32().ShouldBe(1);
        document.LatestAdaptedVersion!.PendingKeyFactCount.ShouldBe(0);
    }

    [Fact]
    public async Task AcknowledgeAll_WhenTheDocumentBelongsToAnotherUser_Returns404()
    {
        await using var factory = new DocumentsApiFactory();
        var foreign = GetDocumentEndpointTests.Seed(factory, OwnerId.From(Guid.NewGuid()), GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsJsonAsync(AcknowledgeAllUri(foreign.Id.Value), new { version = 1 }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreign.LatestAdaptedVersion!.PendingKeyFactCount.ShouldBe(2);
    }

    [Fact]
    public async Task AcknowledgeAll_WhenTheVersionIsNotTheLatest_Returns409()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsJsonAsync(AcknowledgeAllUri(document.Id.Value), new { version = 2 }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        document.LatestAdaptedVersion!.PendingKeyFactCount.ShouldBe(2);
    }

    [Fact]
    public async Task AcknowledgeAll_WhenTheVersionIsMissing_Returns400()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsJsonAsync(AcknowledgeAllUri(document.Id.Value), new { }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        document.LatestAdaptedVersion!.PendingKeyFactCount.ShouldBe(2);
    }

    [Fact]
    public async Task Acknowledge_WhenTheDocumentBelongsToAnotherUser_Returns404()
    {
        await using var factory = new DocumentsApiFactory();
        var foreign = GetDocumentEndpointTests.Seed(factory, OwnerId.From(Guid.NewGuid()), GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsJsonAsync(AcknowledgeUri(foreign.Id.Value, 1), new { version = 1 }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreign.LatestAdaptedVersion!.PendingKeyFactCount.ShouldBe(2);
    }

    [Fact]
    public async Task Acknowledge_WhenTheKeyFactDoesNotExist_Returns404()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsJsonAsync(AcknowledgeUri(document.Id.Value, 9), new { version = 1 }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("code").GetString().ShouldBe("review.key_fact_not_found");
    }

    [Fact]
    public async Task Acknowledge_WhenTheVersionIsMissing_Returns400()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsJsonAsync(AcknowledgeUri(document.Id.Value, 1), new { }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        document.LatestAdaptedVersion!.PendingKeyFactCount.ShouldBe(2);
    }

    private static object Edit(params string[] sentences) => new
    {
        title = "Ayudas para el alquiler",
        sections = new[] { new { question = "¿Quién puede pedirla?", sentences } },
        glossary = new[] { new { term = "Solicitud", definition = "Papel que se rellena para pedir algo." } },
    };

    private static Uri VersionUri(Guid id, int version) => new($"/api/documents/{id}/versions/{version}", UriKind.Relative);

    private static Uri AcknowledgeAllUri(Guid id) => new($"/api/documents/{id}/key-facts/acknowledge", UriKind.Relative);

    private static Uri AcknowledgeUri(Guid id, int factId) => new($"/api/documents/{id}/key-facts/{factId}/acknowledge", UriKind.Relative);

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
}
