// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LecturaFacil.Api.Security;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Api.Tests.Documents;

/// <summary><c>POST /api/documents</c> contra PostgreSQL real (Testcontainers) con las migraciones aplicadas al arrancar.</summary>
public sealed class SubmitDocumentPersistenceTests(SubmitDocumentPersistenceTests.MigratedApiFactory factory)
    : IClassFixture<SubmitDocumentPersistenceTests.MigratedApiFactory>
{
    private const string ValidText = "Texto de prueba suficientemente largo para pasar la validación mínima de caracteres.";

    [Fact]
    public async Task SubmitText_WhenTextIsValid_PersistsAReceivedDocumentOfTheCurrentUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/documents", UriKind.Relative),
            new { title = "Prueba", text = ValidText },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var id = DocumentId.From(json.RootElement.GetProperty("id").GetGuid());

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await context.Set<Document>().AsNoTracking().SingleAsync(document => document.Id == id, cancellationToken);
        stored.OwnerId.ShouldBe(FixedCurrentUser.DevelopmentOwner);
        stored.Status.ShouldBe(DocumentStatus.Received);
        stored.Title.Value.ShouldBe("Prueba");
        stored.OriginalText.Value.ShouldBe(ValidText);
    }

    public sealed class MigratedApiFactory : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        }
    }
}
