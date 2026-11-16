// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace LecturaFacil.Api.Tests.Documents;

/// <summary>
/// La API completa contra PostgreSQL real (Testcontainers), con el worker de adaptación y el proveedor falso:
/// <c>POST</c> del ejemplo de la sección 5 del diseño → worker → <c>GET</c> con la adaptación.
/// </summary>
public sealed class AdaptationFlowTests(AdaptationFlowTests.WorkerApiFactory factory) : IClassFixture<AdaptationFlowTests.WorkerApiFactory>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SubmittedDocument_IsAdaptedInTheBackground_AndCanBeRead()
    {
        using var client = factory.CreateClient();
        using var submitted = await client.PostAsync(
            new Uri("/api/documents", UriKind.Relative),
            new StringContent(await File.ReadAllTextAsync(ExampleFile(), Token), System.Text.Encoding.UTF8, "application/json"),
            Token);
        submitted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var id = (await submitted.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("id").GetGuid();

        var document = await WaitUntilFinishedAsync(client, id);

        document.GetProperty("status").GetString().ShouldBe("Draft");
        var adaptation = document.GetProperty("adaptation");
        adaptation.GetProperty("title").GetString().ShouldBe("Ayudas para pagar el alquiler");
        adaptation.GetProperty("sections").GetArrayLength().ShouldBe(4);
        adaptation.GetProperty("pendingHumanValidation").GetBoolean().ShouldBeTrue();
        var guidelines = adaptation.GetProperty("guidelines");
        guidelines.GetProperty("items").GetArrayLength().ShouldBe(guidelines.GetProperty("totalCount").GetInt32());
        guidelines.GetProperty("summary").GetString().ShouldEndWith("pautas cumplidas");
        var request = factory.Llm.Requests.ShouldHaveSingleItem();
        request.UserMessage.ShouldContain("2,5 veces el IPREM");
        request.SystemPrompt.ShouldContain("Sin siglas sin explicar: ");
    }

    private static async Task<JsonElement> WaitUntilFinishedAsync(HttpClient client, Guid id)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(Timeout);
        while (true)
        {
            var document = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/documents/{id}", UriKind.Relative), timeout.Token);
            if (document.GetProperty("status").GetString() is not ("Received" or "Processing"))
            {
                return document;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }

    private static string ExampleFile()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "fixtures", "ejemplo-iprem.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("No se encuentra tests/fixtures/ejemplo-iprem.json.");
    }

    public sealed class WorkerApiFactory : ApiFactory
    {
        protected override bool RunAdaptationWorker => true;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        }
    }
}
