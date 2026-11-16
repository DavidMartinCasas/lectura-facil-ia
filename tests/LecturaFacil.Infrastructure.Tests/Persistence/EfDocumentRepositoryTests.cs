// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LecturaFacil.Infrastructure.Tests.Persistence;

public sealed class EfDocumentRepositoryTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private static readonly TextLengthLimits Limits = new(10, 1_000);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(Token);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Add_WhenDocumentIsNew_PersistsAllItsFields()
    {
        var owner = OwnerId.From(Guid.NewGuid());
        var createdAt = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var document = Document.Submit(
            owner,
            DocumentTitle.Create("Bases de la convocatoria"),
            OriginalText.Create("Primer párrafo de prueba.\n\nSegundo párrafo de prueba.", Limits),
            createdAt);

        await using (var context = CreateContext())
        {
            await new EfDocumentRepository(context).AddAsync(document, Token);
        }

        await using var readContext = CreateContext();
        var stored = await readContext.Set<Document>().SingleAsync(saved => saved.Id == document.Id, Token);
        stored.OwnerId.ShouldBe(owner);
        stored.Title.Value.ShouldBe("Bases de la convocatoria");
        stored.OriginalText.Value.ShouldBe("Primer párrafo de prueba.\n\nSegundo párrafo de prueba.");
        stored.Status.ShouldBe(DocumentStatus.Received);
        stored.CreatedAt.ShouldBe(createdAt);
        stored.UpdatedAt.ShouldBe(createdAt);
    }

    [Fact]
    public async Task Add_WhenDocumentHasFindings_PersistsThemWithTheDocument()
    {
        var text = OriginalText.Create("Se publica en el BOP el día 15/12/2026.", Limits);
        var document = Document.Submit(OwnerId.From(Guid.NewGuid()), DocumentTitle.Default, text, DateTimeOffset.UtcNow);
        var finding = Finding.At(GuidelineCatalog.Find(GuidelineCodes.Acronyms), text.Value, 17, 20, "Explica la sigla.");
        document.RecordOriginalFindings([finding]);

        await using (var context = CreateContext())
        {
            await new EfDocumentRepository(context).AddAsync(document, Token);
        }

        await using var readContext = CreateContext();
        var stored = await readContext.Set<Document>().SingleAsync(saved => saved.Id == document.Id, Token);
        stored.OriginalFindings.ShouldBe([finding]);
    }

    [Fact]
    public async Task Migrations_SeedTheWholeGuidelineCatalog()
    {
        await using var context = CreateContext();

        var stored = await context.Set<Guideline>().AsNoTracking().ToListAsync(Token);

        stored.ShouldBe(GuidelineCatalog.All, ignoreOrder: true);
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database.ConnectionString).Options);
}
