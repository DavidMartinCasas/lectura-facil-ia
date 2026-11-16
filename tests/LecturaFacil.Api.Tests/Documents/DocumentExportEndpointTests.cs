// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text;
using System.Text.Json;
using LecturaFacil.Api.Tests.Security;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Tests.Documents;

/// <summary>Exportación de la adaptación (US-14), con el repositorio en memoria.</summary>
public sealed class DocumentExportEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "ayudas-para-el-alquiler.docx")]
    [InlineData("pdf", "application/pdf", "ayudas-para-el-alquiler.pdf")]
    [InlineData("md", "text/markdown; charset=utf-8", "ayudas-para-el-alquiler.md")]
    [InlineData("txt", "text/plain; charset=utf-8", "ayudas-para-el-alquiler.txt")]
    public async Task Export_WhenAllWarningsAreReviewed_DownloadsTheFileWithItsTypeAndExtension(string format, string contentType, string fileName)
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Reviewed);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsync(ExportUri(document.Id.Value, format), content: null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.ToString().ShouldBe(contentType);
        var disposition = response.Content.Headers.ContentDisposition!;
        disposition.DispositionType.ShouldBe("attachment");
        disposition.FileName.ShouldBe(fileName);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        (await response.Content.ReadAsByteArrayAsync(Token)).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Export_AsText_ContainsTheAdaptationAndTheDraftNoticeWithoutInternalMetadata()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Reviewed);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsync(ExportUri(document.Id.Value, "txt"), content: null, Token);

        var text = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync(Token));
        text.ShouldStartWith("Ayudas para el alquiler\n");
        text.ShouldContain("¿Quién puede pedirla?\nQuien vive de alquiler.\nLa ayuda paga el 40 % del alquiler.\n");
        text.ShouldContain("Glosario\nSolicitud: Papel que se rellena para pedir algo.\n");
        text.ShouldContain("Borrador · pendiente de validación · ");
        text.ShouldNotContain("Servidor interno");
        text.ShouldNotContain("modelo-secreto");
        text.ShouldNotContain("modelo-verificador-secreto");
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("pdf")]
    [InlineData("md")]
    [InlineData("txt")]
    public async Task Export_WhenAKeyFactWarningIsPending_Returns409(string format)
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, GetDocumentEndpointTests.Adapt);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsync(ExportUri(document.Id.Value, format), content: null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var problem = await ReadJsonAsync(response);
        problem.RootElement.GetProperty("detail").GetString().ShouldBe("Revisa los avisos antes de exportar");
        problem.RootElement.GetProperty("code").GetString().ShouldBe("export.pending_warnings");
    }

    [Theory]
    [InlineData("exe")]
    [InlineData("DOCX")]
    [InlineData("")]
    public async Task Export_WhenTheFormatIsNotSupported_Returns400(string format)
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Reviewed);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsync(ExportUri(document.Id.Value, format), content: null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Export_WithoutFormat_Returns400()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Reviewed);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsync(new Uri($"/api/documents/{document.Id.Value}/export", UriKind.Relative), content: null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Export_WhenTheDocumentBelongsToAnotherUser_Returns404LikeAMissingOne()
    {
        await using var factory = new DocumentsApiFactory();
        var foreign = GetDocumentEndpointTests.Seed(factory, OwnerId.From(Guid.NewGuid()), Reviewed);
        using var client = await factory.CreateApiClientAsync();

        using var foreignResponse = await client.PostAsync(ExportUri(foreign.Id.Value, "docx"), content: null, Token);
        using var missingResponse = await client.PostAsync(ExportUri(Guid.NewGuid(), "docx"), content: null, Token);

        foreignResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        GetDocumentEndpointTests.WithoutTraceId(await foreignResponse.Content.ReadAsStringAsync(Token))
            .ShouldBe(GetDocumentEndpointTests.WithoutTraceId(await missingResponse.Content.ReadAsStringAsync(Token)));
    }

    [Fact]
    public async Task Export_WhenThereIsNoAdaptationYet_Returns409()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsync(ExportUri(document.Id.Value, "txt"), content: null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Export_WhenTheExportLimitIsReached_Returns429()
    {
        await using var factory = new DocumentsApiFactory();
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Limits:ExportsPerMinute", "2"));
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Reviewed);
        using var client = await limited.CreateApiClientAsync();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var response = await client.PostAsync(ExportUri(document.Id.Value, "txt"), content: null, Token);
            statuses.Add(response.StatusCode);
        }

        statuses.ShouldBe([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests]);
    }

    /// <summary>Adaptado y con todos los avisos de datos clave marcados como revisados.</summary>
    private static void Reviewed(Document document)
    {
        GetDocumentEndpointTests.Adapt(document);
        document.AcknowledgeKeyFact(1, Now);
        document.AcknowledgeKeyFact(2, Now);
    }

    private static Uri ExportUri(Guid id, string format) =>
        new($"/api/documents/{id}/export?format={Uri.EscapeDataString(format)}", UriKind.Relative);

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(Token), cancellationToken: Token);
}
