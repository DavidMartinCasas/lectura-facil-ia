// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text.Json;
using LecturaFacil.Api.Tests.Security;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Tests.Documents;

public sealed class RetryAdaptationEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Retry_WhenTheAdaptationFailed_Returns202AndTheDocumentProcessingAgain()
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Fail("llm.provider_unavailable"));
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsync(RetryUri(document.Id.Value), null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        json.RootElement.GetProperty("status").GetString().ShouldBe("Processing");
        json.RootElement.GetProperty("error").ValueKind.ShouldBe(JsonValueKind.Null);
        json.RootElement.GetProperty("canRetry").GetBoolean().ShouldBeFalse();
        document.Status.ShouldBe(DocumentStatus.Processing);
    }

    [Fact]
    public async Task Get_WhenTheAdaptationFailed_SaysWhetherItCanBeRetried()
    {
        await using var factory = new DocumentsApiFactory();
        var retriable = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Fail("llm.provider_unavailable"));
        var tooLong = GetDocumentEndpointTests.Seed(factory, TestUsers.DefaultOwner, Fail("llm.input_too_long"));
        using var client = await factory.CreateApiClientAsync();

        (await CanRetryAsync(client, retriable.Id.Value)).ShouldBeTrue();
        (await CanRetryAsync(client, tooLong.Id.Value)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("llm.input_too_long")]
    [InlineData(null)]
    public async Task Retry_WhenRetryingCannotHelp_Returns409AndChangesNothing(string? failureCode)
    {
        await using var factory = new DocumentsApiFactory();
        var document = GetDocumentEndpointTests.Seed(
            factory,
            TestUsers.DefaultOwner,
            failureCode is null ? GetDocumentEndpointTests.Adapt : Fail(failureCode));
        var status = document.Status;
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.PostAsync(RetryUri(document.Id.Value), null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        document.Status.ShouldBe(status);
    }

    private static async Task<bool> CanRetryAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(new Uri($"/api/documents/{id}", UriKind.Relative), Token);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        return json.RootElement.GetProperty("canRetry").GetBoolean();
    }

    private static Action<Document> Fail(string failureCode) => document =>
    {
        document.StartProcessing(Now);
        document.FailProcessing(failureCode, Now);
    };

    private static Uri RetryUri(Guid id) => new($"/api/documents/{id}/retry", UriKind.Relative);
}
