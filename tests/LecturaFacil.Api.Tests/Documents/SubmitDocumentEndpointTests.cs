// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LecturaFacil.Api.Security;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Testing;

namespace LecturaFacil.Api.Tests.Documents;

/// <summary><c>POST /api/documents</c> sin base de datos (repositorio en memoria). La persistencia real se prueba aparte.</summary>
public sealed class SubmitDocumentEndpointTests
{
    private const string ValidText = "Texto de prueba suficientemente largo para pasar la validación mínima de caracteres.";
    private const int MaxFileSizeBytes = 5 * 1024 * 1024;

    private static readonly Uri DocumentsUri = new("/api/documents", UriKind.Relative);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SubmitText_WhenTextIsValid_Returns202WithTheIdOfAReceivedDocument()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(DocumentsUri, new { title = "Prueba", text = ValidText }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using var json = await ReadJsonAsync(response);
        var id = json.RootElement.GetProperty("id").GetGuid();
        json.RootElement.GetProperty("status").GetString().ShouldBe("Received");
        var document = factory.Repository.Added.ShouldHaveSingleItem();
        document.Id.Value.ShouldBe(id);
        document.Status.ShouldBe(DocumentStatus.Received);
        document.Title.Value.ShouldBe("Prueba");
        document.OriginalText.Value.ShouldBe(ValidText);
        document.OwnerId.ShouldBe(FixedCurrentUser.DevelopmentOwner);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(20_000)]
    public async Task SubmitText_WhenLengthIsAtTheLimits_Returns202(int length)
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(DocumentsUri, new { text = new string('a', length) }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Repository.Added.ShouldHaveSingleItem().Title.ShouldBe(DocumentTitle.Default);
    }

    [Theory]
    [InlineData(49, "al menos 50")]
    [InlineData(20_001, "20000")]
    public async Task SubmitText_WhenLengthIsOutOfTheLimits_Returns400AndStoresNothing(int length, string expectedDetail)
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(DocumentsUri, new { text = new string('a', length) }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync(Token));
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain(expectedDetail);
        factory.Repository.Added.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"text\":\"\"}")]
    [InlineData("null")]
    public async Task SubmitText_WhenTextIsMissing_Returns400ValidationProblem(string body)
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(DocumentsUri, content, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errors").GetProperty("Text").GetArrayLength().ShouldBe(1);
        factory.Repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitText_WhenJsonIsMalformed_Returns400WithoutInternalDetails()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{\"text\": ", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(DocumentsUri, content, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(Token);
        body.ShouldNotContain("JsonException");
        body.ShouldNotContain("System.");
    }

    [Fact]
    public async Task Submit_WhenContentTypeIsNotSupported_Returns415()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(ValidText, Encoding.UTF8, "text/plain");

        using var response = await client.PostAsync(DocumentsUri, content, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        factory.Repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitPdf_WhenPdfIsDigital_Returns202AndStoresTheTextWithItsParagraphs()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostFileAsync(client, PdfFixtures.Read(PdfFixtures.Normal), "Bases ayudas.pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var document = factory.Repository.Added.ShouldHaveSingleItem();
        document.Status.ShouldBe(DocumentStatus.Received);
        document.Title.Value.ShouldBe("Bases ayudas");
        var paragraphs = document.OriginalText.Value.Split("\n\n");
        paragraphs.Length.ShouldBe(3);
        paragraphs[0].ShouldStartWith("Convocatoria de ayudas de ejemplo");
        paragraphs[2].ShouldEndWith("300 euros por persona.");
    }

    [Fact]
    public async Task SubmitPdf_WhenPdfHasNoText_Returns422WithTheScannedMessage()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostFileAsync(client, PdfFixtures.Read(PdfFixtures.WithoutText), "escaneado.pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("detail").GetString()
            .ShouldBe("Este PDF parece escaneado. De momento solo se admiten PDF con texto.");
        factory.Repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitPdf_WhenPdfIsCorrupt_Returns422WithoutInternalDetails()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostFileAsync(client, PdfFixtures.Read(PdfFixtures.Corrupt), "documento.pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync(Token);
        body.ShouldNotContain("PdfPig");
        body.ShouldNotContain("Exception");
        factory.Repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitPdf_WhenFileIsNamedPdfButSignatureIsNot_Returns400AndStoresNothing()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostFileAsync(client, PdfFixtures.FakePdf(), "documento.pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("code").GetString().ShouldBe("document.invalid_file_type");
        factory.Repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitPdf_WhenFileIsJustAboveTheMaximumSize_Returns413AndDoesNotProcessIt()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostFileAsync(client, LargePdf(MaxFileSizeBytes + 1), "grande.pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain("5 MB");
        factory.Repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitPdf_WhenRequestIsFarAboveTheMaximumSize_Returns413BeforeReadingTheBody()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostFileAsync(client, LargePdf(MaxFileSizeBytes * 2), "enorme.pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        factory.Repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitPdf_WhenFormHasNoFile_Returns400ValidationProblem()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent { { new StringContent("hola"), "otro" } };

        using var response = await client.PostAsync(DocumentsUri, form, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("errors").GetProperty("file").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task SubmitPdf_WhenFileIsEmpty_Returns400ValidationProblem()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostFileAsync(client, [], "vacio.pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        factory.Repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task OpenApi_InDevelopment_DescribesTheSubmitEndpoint()
    {
        await using var factory = new DocumentsApiFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        var operation = json.RootElement.GetProperty("paths").GetProperty("/api/documents").GetProperty("post");
        var content = operation.GetProperty("requestBody").GetProperty("content");
        content.TryGetProperty("application/json", out _).ShouldBeTrue();
        content.TryGetProperty("multipart/form-data", out _).ShouldBeTrue();
        var responses = operation.GetProperty("responses");
        foreach (var status in new[] { "202", "400", "413", "422" })
        {
            responses.TryGetProperty(status, out _).ShouldBeTrue($"Falta la respuesta {status}");
        }
    }

    private static async Task<HttpResponseMessage> PostFileAsync(HttpClient client, byte[] bytes, string fileName)
    {
        using var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        using var form = new MultipartFormDataContent { { file, "file", fileName } };
        return await client.PostAsync(DocumentsUri, form, Token);
    }

    private static byte[] LargePdf(int size)
    {
        var bytes = new byte[size];
        "%PDF-1.7\n"u8.CopyTo(bytes);
        return bytes;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
}
