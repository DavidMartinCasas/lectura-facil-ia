// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text.Json;
using LecturaFacil.Api.Security;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Api.Tests.Documents;

/// <summary><c>GET /api/documents/{id}</c> y su SSE, con el repositorio en memoria.</summary>
public sealed class GetDocumentEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_WhenTheDocumentIsReceived_ReturnsItsStatusWithoutAdaptation()
    {
        await using var factory = new DocumentsApiFactory();
        var document = Seed(factory, FixedCurrentUser.DevelopmentOwner);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(DocumentUri(document.Id.Value), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("id").GetGuid().ShouldBe(document.Id.Value);
        json.RootElement.GetProperty("status").GetString().ShouldBe("Received");
        json.RootElement.GetProperty("adaptation").ValueKind.ShouldBe(JsonValueKind.Null);
        json.RootElement.GetProperty("error").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Get_WhenTheDocumentIsInDraft_ReturnsTheAdaptationAsAPendingDraftWithoutModelNames()
    {
        await using var factory = new DocumentsApiFactory();
        var document = Seed(factory, FixedCurrentUser.DevelopmentOwner, Adapt);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(DocumentUri(document.Id.Value), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(Token);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetString().ShouldBe("Draft");
        var adaptation = json.RootElement.GetProperty("adaptation");
        adaptation.GetProperty("version").GetInt32().ShouldBe(1);
        adaptation.GetProperty("title").GetString().ShouldBe("Ayudas para el alquiler");
        adaptation.GetProperty("pendingHumanValidation").GetBoolean().ShouldBeTrue();
        adaptation.GetProperty("promptVersion").GetString().ShouldBe("adapter.v1");
        var section = adaptation.GetProperty("sections")[0];
        section.GetProperty("question").GetString().ShouldBe("¿Quién puede pedirla?");
        section.GetProperty("sentences")[0].GetString().ShouldBe("Quien vive de alquiler.");
        adaptation.GetProperty("glossary")[0].GetProperty("term").GetString().ShouldBe("Solicitud");
        body.ShouldNotContain("modelo-secreto");
        body.ShouldNotContain("Servidor interno");
    }

    [Fact]
    public async Task Get_WhenTheDocumentIsInDraft_IncludesTheGuidelineReportOfTheAdaptation()
    {
        await using var factory = new DocumentsApiFactory();
        var document = Seed(factory, FixedCurrentUser.DevelopmentOwner, Adapt);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(DocumentUri(document.Id.Value), Token);

        using var json = await ReadJsonAsync(response);
        var report = json.RootElement.GetProperty("adaptation").GetProperty("guidelines");
        var total = GuidelineCatalog.All.Count;
        report.GetProperty("summary").GetString().ShouldBe($"{total - 1} de {total} pautas cumplidas");
        report.GetProperty("compliantCount").GetInt32().ShouldBe(total - 1);
        report.GetProperty("totalCount").GetInt32().ShouldBe(total);
        var items = report.GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBe(total);
        var percentages = items.Single(item => item.GetProperty("code").GetString() == GuidelineCodes.Percentages);
        percentages.GetProperty("description").GetString().ShouldBe("Sin porcentajes");
        percentages.GetProperty("status").GetString().ShouldBe("NotCompliant");
        percentages.GetProperty("severity").GetString().ShouldBe("Medium");
        percentages.GetProperty("findingCount").GetInt32().ShouldBe(1);
        var finding = percentages.GetProperty("findings").EnumerateArray().ShouldHaveSingleItem();
        finding.GetProperty("fragment").GetString().ShouldBe("40 %");
        finding.GetProperty("start").GetInt32().ShouldBe(
            "Ayudas para el alquiler\n¿Quién puede pedirla?\nQuien vive de alquiler.\nLa ayuda paga el ".Length);
        finding.GetProperty("end").GetInt32().ShouldBe(finding.GetProperty("start").GetInt32() + 4);
        finding.GetProperty("suggestion").GetString().ShouldBe("Explica la cantidad sin porcentajes.");
        items.Where(item => item.GetProperty("code").GetString() != GuidelineCodes.Percentages)
            .ShouldAllBe(item => item.GetProperty("status").GetString() == "Compliant");
    }

    [Fact]
    public async Task Get_WhenTheAdaptationFailed_ReturnsAGenericReason()
    {
        await using var factory = new DocumentsApiFactory();
        var document = Seed(factory, FixedCurrentUser.DevelopmentOwner, seeded =>
        {
            seeded.StartProcessing(Now);
            seeded.FailProcessing("llm.invalid_provider_output", Now);
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(DocumentUri(document.Id.Value), Token);

        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("status").GetString().ShouldBe("Error");
        var error = json.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("llm.invalid_provider_output");
        error.GetProperty("message").GetString().ShouldBe("El servicio de IA ha devuelto una respuesta que no se puede usar. Inténtalo de nuevo.");
    }

    [Fact]
    public async Task Get_WhenTheDocumentBelongsToAnotherUser_Returns404LikeAMissingOne()
    {
        await using var factory = new DocumentsApiFactory();
        var foreign = Seed(factory, OwnerId.From(Guid.NewGuid()), Adapt);
        using var client = factory.CreateClient();

        using var foreignResponse = await client.GetAsync(DocumentUri(foreign.Id.Value), Token);
        using var missingResponse = await client.GetAsync(DocumentUri(Guid.NewGuid()), Token);

        foreignResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var foreignBody = await foreignResponse.Content.ReadAsStringAsync(Token);
        WithoutTraceId(foreignBody).ShouldBe(WithoutTraceId(await missingResponse.Content.ReadAsStringAsync(Token)));
        foreignBody.ShouldNotContain("Ayudas");
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("no-es-un-guid")]
    public async Task Get_WhenTheIdIsNotValid_Returns404(string id)
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"/api/documents/{id}", UriKind.Relative), Token);
        using var events = await client.GetAsync(new Uri($"/api/documents/{id}/events", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        events.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Events_WhenTheDocumentIsFinished_SendsItsStatusAndCloses()
    {
        await using var factory = new DocumentsApiFactory();
        var document = Seed(factory, FixedCurrentUser.DevelopmentOwner, Adapt);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(EventsUri(document.Id.Value), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        var body = await response.Content.ReadAsStringAsync(Token);
        body.ShouldContain("event: status");
        body.ShouldContain("\"status\":\"Draft\"");
        body.ShouldContain($"\"id\":\"{document.Id.Value}\"");
    }

    [Fact]
    public async Task Events_WhenTheDocumentBelongsToAnotherUser_Returns404()
    {
        await using var factory = new DocumentsApiFactory();
        var foreign = Seed(factory, OwnerId.From(Guid.NewGuid()));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(EventsUri(foreign.Id.Value), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static void Adapt(Document document)
    {
        var content = AdaptedContent.TryCreate(
            "Ayudas para el alquiler",
            [new AdaptedSectionDraft("¿Quién puede pedirla?", ["Quien vive de alquiler.", "La ayuda paga el 40 % del alquiler."])],
            [new GlossaryTermDraft("Solicitud", "Papel que se rellena para pedir algo.")])!;
        var start = content.PlainText.IndexOf("40 %", StringComparison.Ordinal);
        var percentage = Finding.At(
            GuidelineCatalog.Find(GuidelineCodes.Percentages), content.PlainText, start, start + 4, "Explica la cantidad sin porcentajes.");
        document.StartProcessing(Now);
        document.CompleteAdaptation(
            content,
            new AdaptationMetadata("Servidor interno", "modelo-secreto", "adapter.v1", 10, 20, TimeSpan.FromSeconds(1)),
            GuidelineReport.Create(new GuidelineAnalysis([percentage], []), [], 10),
            Now);
    }

    private static Document Seed(DocumentsApiFactory factory, OwnerId owner, Action<Document>? prepare = null)
    {
        var document = Document.Submit(
            owner,
            DocumentTitle.Create("Bases"),
            OriginalText.Create("Texto de prueba suficientemente largo.", new TextLengthLimits(10, 1_000)),
            Now);
        prepare?.Invoke(document);
        factory.Repository.AddAsync(document, Token).GetAwaiter().GetResult();
        return document;
    }

    private static string WithoutTraceId(string problem)
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(problem)!.AsObject();
        json.Remove("traceId");
        return json.ToJsonString();
    }

    private static Uri DocumentUri(Guid id) => new($"/api/documents/{id}", UriKind.Relative);

    private static Uri EventsUri(Guid id) => new($"/api/documents/{id}/events", UriKind.Relative);

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
}
