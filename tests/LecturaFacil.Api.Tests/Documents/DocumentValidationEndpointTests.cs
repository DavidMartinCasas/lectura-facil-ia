// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LecturaFacil.Api.Tests.Security;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Tests.Documents;

/// <summary>Validación humana por HTTP: enviar, anotar el resultado, retirar y publicar.</summary>
public sealed class DocumentValidationEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheWholeCycle_SendRecordAndPublish_EndsPublishedWithItsValidation()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Reviewed);
        using var client = await factory.CreateApiClientAsync();
        var id = document.Id.Value;

        (await CanSendAsync(client, id)).ShouldBeTrue();

        using (var sent = await client.PostAsJsonAsync(Uri(id, "validation/send"), new { version = 1 }, Token))
        {
            sent.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var json = await JsonAsync(sent);
            json.RootElement.GetProperty("status").GetString().ShouldBe("PendingValidation");
            json.RootElement.GetProperty("editable").GetBoolean().ShouldBeFalse();
            json.RootElement.GetProperty("canSendToValidation").GetBoolean().ShouldBeFalse();
            json.RootElement.GetProperty("validation").ValueKind.ShouldBe(JsonValueKind.Null);
        }

        using (var recorded = await client.PostAsJsonAsync(
            Uri(id, "validation"),
            new { version = 1, outcome = "Approved", validatedBy = "Grupo de lectura", notes = "Se entiende bien." },
            Token))
        {
            recorded.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var json = await JsonAsync(recorded);
            json.RootElement.GetProperty("status").GetString().ShouldBe("Validated");
            json.RootElement.GetProperty("adaptation").GetProperty("pendingHumanValidation").GetBoolean().ShouldBeFalse();
            var validation = json.RootElement.GetProperty("validation");
            validation.GetProperty("version").GetInt32().ShouldBe(1);
            validation.GetProperty("outcome").GetString().ShouldBe("Approved");
            validation.GetProperty("validatedBy").GetString().ShouldBe("Grupo de lectura");
            validation.GetProperty("notes").GetString().ShouldBe("Se entiende bien.");
        }

        using (var published = await client.PostAsync(Uri(id, "publish"), null, Token))
        {
            published.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var json = await JsonAsync(published);
            json.RootElement.GetProperty("status").GetString().ShouldBe("Published");
        }

        document.Status.ShouldBe(DocumentStatus.Published);
        document.Validations.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Send_WithKeyFactsToReview_Returns409AndAsksToReviewThem()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        (await CanSendAsync(client, document.Id.Value)).ShouldBeFalse();
        using var response = await client.PostAsJsonAsync(Uri(document.Id.Value, "validation/send"), new { version = 1 }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain("validation.pending_warnings");
        document.Status.ShouldBe(DocumentStatus.Draft);
    }

    [Fact]
    public async Task Record_ChangesRequested_ReturnsItToAnEditableDraft()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Pending);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsJsonAsync(
            Uri(document.Id.Value, "validation"),
            new { version = 1, outcome = "ChangesRequested", validatedBy = "Grupo" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await JsonAsync(response);
        json.RootElement.GetProperty("status").GetString().ShouldBe("Draft");
        json.RootElement.GetProperty("editable").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("validation").GetProperty("notes").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(1, null, "Grupo")]
    [InlineData(1, "Maybe", "Grupo")]
    [InlineData(1, "0", "Grupo")]
    [InlineData(1, "Approved", "   ")]
    [InlineData(1, "Approved", null)]
    [InlineData(null, "Approved", "Grupo")]
    [InlineData(0, "Approved", "Grupo")]
    public async Task Record_WithAnInvalidRequest_Returns400AndChangesNothing(int? version, string? outcome, string? validatedBy)
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Pending);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsJsonAsync(
            Uri(document.Id.Value, "validation"),
            new { version, outcome, validatedBy },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        document.Status.ShouldBe(DocumentStatus.PendingValidation);
    }

    [Fact]
    public async Task Record_ForAnotherVersionThanThePendingOne_Returns409AndChangesNothing()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Pending);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsJsonAsync(
            Uri(document.Id.Value, "validation"),
            new { version = 2, outcome = "Approved", validatedBy = "Grupo" },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        document.Status.ShouldBe(DocumentStatus.PendingValidation);
        document.Validations.ShouldBeEmpty();
    }

    [Fact]
    public async Task Record_WithTooLongNotes_Returns400()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Pending);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsJsonAsync(
            Uri(document.Id.Value, "validation"),
            new { version = 1, outcome = "Approved", validatedBy = "Grupo", notes = new string('a', ValidationRecord.NotesMaxLength + 1) },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Withdraw_ReturnsItToDraft_AndPublish_OfANotValidatedDocument_Returns409()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Pending);
        using var client = await factory.CreateApiClientAsync();

        using var publish = await client.PostAsync(Uri(document.Id.Value, "publish"), null, Token);
        using var withdraw = await client.PostAsync(Uri(document.Id.Value, "validation/withdraw"), null, Token);
        using var again = await client.PostAsync(Uri(document.Id.Value, "validation/withdraw"), null, Token);

        publish.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        withdraw.StatusCode.ShouldBe(HttpStatusCode.OK);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        document.Status.ShouldBe(DocumentStatus.Draft);
    }

    internal static void Reviewed(Document document)
    {
        GetDocumentEndpointTests.Adapt(document);
        document.AcknowledgeAllKeyFacts(Now);
    }

    internal static void Pending(Document document)
    {
        Reviewed(document);
        document.SendToValidation(Now);
    }

    private static async Task<bool> CanSendAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(new Uri($"/api/documents/{id}", UriKind.Relative), Token);
        using var json = await JsonAsync(response);
        return json.RootElement.GetProperty("canSendToValidation").GetBoolean();
    }

    private static async Task<JsonDocument> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

    private static Uri Uri(Guid id, string path) => new($"/api/documents/{id}/{path}", UriKind.Relative);
}
