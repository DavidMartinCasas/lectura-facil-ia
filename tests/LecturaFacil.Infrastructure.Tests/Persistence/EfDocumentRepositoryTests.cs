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

    [Fact]
    public async Task Save_WhenTheDocumentIsAdapted_PersistsTheVersionAndTheNewStatus()
    {
        var document = await StoreAsync(OwnerId.From(Guid.NewGuid()));
        var content = AdaptedContent.TryCreate(
            "Ayudas",
            [new AdaptedSectionDraft("¿Quién?", ["Primera frase.", "Segunda frase."])],
            [new GlossaryTermDraft("Solicitud", "Papel para pedir algo.")])!;
        var metadata = new AdaptationMetadata("Proveedor", "modelo", "adapter.v1", 10, 20, TimeSpan.FromMilliseconds(1500));
        var finding = Finding.At(GuidelineCatalog.Find(GuidelineCodes.ShortSentences), content.PlainText, 0, 6, "Divide la frase.");
        var report = GuidelineReport.Create(new GuidelineAnalysis([finding], [GuidelineCodes.PassiveVoice]), [], 10);

        await using (var context = CreateContext())
        {
            var repository = new EfDocumentRepository(context);
            var tracked = (await repository.FindAsync(document.Id, Token))!;
            tracked.StartProcessing(DateTimeOffset.UtcNow);
            await repository.SaveAsync(tracked, Token);
            tracked.CompleteAdaptation(content, metadata, report, DateTimeOffset.UtcNow);
            await repository.SaveAsync(tracked, Token);
        }

        await using var readContext = CreateContext();
        var stored = (await new EfDocumentRepository(readContext).FindForOwnerAsync(document.Id, document.OwnerId, Token))!;
        stored.Status.ShouldBe(DocumentStatus.Draft);
        var version = stored.AdaptedVersions.ShouldHaveSingleItem();
        version.Title.ShouldBe("Ayudas");
        version.Sections.ShouldHaveSingleItem().Sentences.ShouldBe(["Primera frase.", "Segunda frase."]);
        version.Glossary.ShouldBe(content.Glossary);
        version.Metadata.ShouldBe(metadata);
        var storedReport = version.GuidelineReport.ShouldNotBeNull();
        storedReport.Results.ShouldBe(report.Results);
        storedReport.Find(GuidelineCodes.PassiveVoice).Status.ShouldBe(GuidelineStatus.NotChecked);
        storedReport.Find(GuidelineCodes.ShortSentences).Findings.ShouldBe([finding]);
    }

    [Fact]
    public async Task Save_WhenTheAdaptationFails_PersistsTheFailureCode()
    {
        var document = await StoreAsync(OwnerId.From(Guid.NewGuid()));

        await using (var context = CreateContext())
        {
            var repository = new EfDocumentRepository(context);
            var tracked = (await repository.FindAsync(document.Id, Token))!;
            tracked.StartProcessing(DateTimeOffset.UtcNow);
            tracked.FailProcessing("llm.provider_unavailable", DateTimeOffset.UtcNow);
            await repository.SaveAsync(tracked, Token);
        }

        await using var readContext = CreateContext();
        var stored = (await new EfDocumentRepository(readContext).FindAsync(document.Id, Token))!;
        stored.Status.ShouldBe(DocumentStatus.Error);
        stored.FailureCode.ShouldBe("llm.provider_unavailable");
    }

    [Fact]
    public async Task FindForOwner_WhenTheDocumentBelongsToSomeoneElse_ReturnsNull()
    {
        var document = await StoreAsync(OwnerId.From(Guid.NewGuid()));
        await using var context = CreateContext();
        var repository = new EfDocumentRepository(context);
        var stranger = OwnerId.From(Guid.NewGuid());

        (await repository.FindForOwnerAsync(document.Id, stranger, Token)).ShouldBeNull();
        (await repository.FindStatusForOwnerAsync(document.Id, stranger, Token)).ShouldBeNull();
        (await repository.FindStatusForOwnerAsync(document.Id, document.OwnerId, Token)).ShouldBe(DocumentStatus.Received);
    }

    [Fact]
    public async Task ListAwaitingAdaptation_ReturnsReceivedAndProcessingDocumentsOnly()
    {
        var received = await StoreAsync(OwnerId.From(Guid.NewGuid()));
        var processing = await StoreAsync(OwnerId.From(Guid.NewGuid()), document => document.StartProcessing(DateTimeOffset.UtcNow));
        var failed = await StoreAsync(OwnerId.From(Guid.NewGuid()), document =>
        {
            document.StartProcessing(DateTimeOffset.UtcNow);
            document.FailProcessing("llm.provider_unavailable", DateTimeOffset.UtcNow);
        });
        await using var context = CreateContext();

        var awaiting = await new EfDocumentRepository(context).ListAwaitingAdaptationAsync(Token);

        awaiting.ShouldContain(received.Id);
        awaiting.ShouldContain(processing.Id);
        awaiting.ShouldNotContain(failed.Id);
    }

    private async Task<Document> StoreAsync(OwnerId owner, Action<Document>? prepare = null)
    {
        var document = Document.Submit(
            owner,
            DocumentTitle.Default,
            OriginalText.Create("Texto de prueba del documento.", Limits),
            DateTimeOffset.UtcNow);
        prepare?.Invoke(document);
        await using var context = CreateContext();
        await new EfDocumentRepository(context).AddAsync(document, Token);
        return document;
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database.ConnectionString).Options);
}
