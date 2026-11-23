// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;
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
        var verification = MeaningVerification.Combine(
            "Un máximo de 250 € y el 40 % de la renta.",
            "Como mucho, 250 euros.",
            [KeyFact.Create(KeyFactType.Amount, "máximo de 250 €", "Como mucho, 250 euros", KeyFactVerdict.Conserved)],
            new VerificationMetadata("Proveedor", "modelo-verificador", "verify.v1"),
            10);

        await using (var context = CreateContext())
        {
            var repository = new EfDocumentRepository(context);
            var tracked = (await repository.FindAsync(document.Id, Token))!;
            tracked.StartProcessing(DateTimeOffset.UtcNow);
            await repository.SaveAsync(tracked, Token);
            tracked.CompleteAdaptation(content, metadata, report, verification, DateTimeOffset.UtcNow);
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
        var storedVerification = version.MeaningVerification.ShouldNotBeNull();
        storedVerification.Status.ShouldBe(VerificationStatus.Verified);
        storedVerification.Metadata.ShouldBe(verification.Metadata);
        storedVerification.KeyFacts.ShouldBe(verification.KeyFacts);
        storedVerification.KeyFacts[0].AdaptedValue.ShouldBeNull();
    }

    [Fact]
    public async Task Save_WhenAKeyFactIsAcknowledgedAndTheDraftIsEdited_PersistsTheMarkAndTheNewVersion()
    {
        var content = AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Quién?", ["Primera frase."])], [])!;
        var metadata = new AdaptationMetadata("Servidor propio", "modelo", "adapter.v1", 10, 20, TimeSpan.FromSeconds(1));
        var report = GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10);
        var verification = MeaningVerification.Restore(
            VerificationStatus.Verified,
            [KeyFact.Create(KeyFactType.Amount, "250 €", null, new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation))],
            null);
        var document = await StoreAsync(OwnerId.From(Guid.NewGuid()), seeded =>
        {
            seeded.StartProcessing(DateTimeOffset.UtcNow);
            seeded.CompleteAdaptation(content, metadata, report, verification, DateTimeOffset.UtcNow);
        });

        await using (var context = CreateContext())
        {
            var repository = new EfDocumentRepository(context);
            (await repository.FindForOwnerToUpdateAsync(document.Id, OwnerId.From(Guid.NewGuid()), Token)).ShouldBeNull();
            var tracked = (await repository.FindForOwnerToUpdateAsync(document.Id, document.OwnerId, Token))!;
            tracked.AcknowledgeKeyFact(0, DateTimeOffset.UtcNow);
            await repository.SaveAsync(tracked, Token);
        }

        await using (var context = CreateContext())
        {
            var repository = new EfDocumentRepository(context);
            var tracked = (await repository.FindForOwnerToUpdateAsync(document.Id, document.OwnerId, Token))!;
            tracked.LatestAdaptedVersion!.MeaningVerification!.KeyFacts[0].Acknowledged.ShouldBeTrue();
            var edited = AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Quién?", ["Frase editada."])], [])!;
            tracked.SaveManualEdit(edited, metadata with { PromptVersion = "manual" }, report, tracked.LatestAdaptedVersion.MeaningVerification, DateTimeOffset.UtcNow);
            await repository.SaveAsync(tracked, Token);
        }

        await using var readContext = CreateContext();
        var stored = (await new EfDocumentRepository(readContext).FindForOwnerAsync(document.Id, document.OwnerId, Token))!;
        stored.AdaptedVersions.Select(version => version.Number).ShouldBe([1, 2]);
        stored.AdaptedVersions[0].Sections[0].Sentences.ShouldBe(["Primera frase."]);
        stored.AdaptedVersions[0].MeaningVerification!.KeyFacts[0].Acknowledged.ShouldBeTrue();
        stored.LatestAdaptedVersion!.Sections[0].Sentences.ShouldBe(["Frase editada."]);
        stored.LatestAdaptedVersion.Metadata.PromptVersion.ShouldBe("manual");
        stored.LatestAdaptedVersion.PendingKeyFactCount.ShouldBe(0);
        stored.Status.ShouldBe(DocumentStatus.Draft);
    }

    [Fact]
    public async Task Save_WhenAnotherRequestChangedTheDocumentMeanwhile_ThrowsAndKeepsTheFirstChange()
    {
        var verification = MeaningVerification.Restore(
            VerificationStatus.Verified,
            [
                KeyFact.Create(KeyFactType.Amount, "250 €", null, new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation)),
                KeyFact.Create(KeyFactType.Deadline, "15 días", null, new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation)),
            ],
            null);
        var document = await StoreAsync(OwnerId.From(Guid.NewGuid()), seeded =>
        {
            seeded.StartProcessing(DateTimeOffset.UtcNow);
            seeded.CompleteAdaptation(
                AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Quién?", ["Una frase."])], [])!,
                new AdaptationMetadata("Servidor propio", "modelo", "adapter.v1", 1, 1, TimeSpan.FromSeconds(1)),
                GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10),
                verification,
                DateTimeOffset.UtcNow);
        });
        await using var first = CreateContext();
        await using var second = CreateContext();
        var firstDocument = (await new EfDocumentRepository(first).FindForOwnerToUpdateAsync(document.Id, document.OwnerId, Token))!;
        var secondDocument = (await new EfDocumentRepository(second).FindForOwnerToUpdateAsync(document.Id, document.OwnerId, Token))!;

        firstDocument.AcknowledgeKeyFact(0, DateTimeOffset.UtcNow.AddSeconds(1));
        await new EfDocumentRepository(first).SaveAsync(firstDocument, Token);
        secondDocument.AcknowledgeKeyFact(1, DateTimeOffset.UtcNow.AddSeconds(2));

        await Should.ThrowAsync<ConcurrentUpdateException>(() => new EfDocumentRepository(second).SaveAsync(secondDocument, Token));
        await using var readContext = CreateContext();
        var stored = (await new EfDocumentRepository(readContext).FindForOwnerAsync(document.Id, document.OwnerId, Token))!;
        stored.LatestAdaptedVersion!.MeaningVerification!.KeyFacts.Select(fact => fact.Acknowledged).ShouldBe([true, false]);
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
        (await repository.FindProgressForOwnerAsync(document.Id, stranger, Token)).ShouldBeNull();
        (await repository.FindProgressForOwnerAsync(document.Id, document.OwnerId, Token))
            .ShouldBe(new DocumentProgress(DocumentStatus.Received, null));
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

    [Fact]
    public async Task ListForOwner_ReturnsOnlyTheOwnerDocuments_NewestFirst_WithTheProviderOfTheLatestVersion()
    {
        var owner = OwnerId.From(Guid.NewGuid());
        var createdAt = new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
        var received = await StoreAsync(owner, createdAt: createdAt);
        var adapted = await StoreAsync(owner, document =>
        {
            document.StartProcessing(createdAt.AddMinutes(1));
            document.CompleteAdaptation(
                AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Quién?", ["Una frase."])], [])!,
                new AdaptationMetadata("Servidor local", "modelo", "adapter.v1", 1, 1, TimeSpan.FromSeconds(1)),
                GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10),
                MeaningVerification.NotVerified("Texto de prueba.", 10),
                createdAt.AddMinutes(2));
        }, createdAt.AddMinutes(1));
        await StoreAsync(OwnerId.From(Guid.NewGuid()), createdAt: createdAt.AddMinutes(5));
        await using var context = CreateContext();

        var page = await new EfDocumentRepository(context).ListForOwnerAsync(owner, Query(), Token);

        var summaries = page.Items;
        summaries.Select(summary => summary.Id).ShouldBe([adapted.Id, received.Id]);
        summaries[0].Status.ShouldBe(DocumentStatus.Draft);
        summaries[0].Provider.ShouldBe("Servidor local");
        summaries[0].PendingKeyFactCount.ShouldBe(0);
        summaries[0].BrokenGuidelineCount.ShouldNotBeNull();
        summaries[0].ReadabilityTexts.ShouldBe(new ReadabilityTexts("Texto de prueba del documento.", "Ayudas\n¿Quién?\nUna frase."));
        summaries[1].PendingKeyFactCount.ShouldBeNull();
        summaries[1].BrokenGuidelineCount.ShouldBeNull();
        summaries[1].ReadabilityTexts.ShouldBeNull();
        summaries[0].Title.ShouldBe(DocumentTitle.Default.Value);
        summaries[1].Provider.ShouldBeNull();
        page.Next.ShouldBeNull();
    }

    [Fact]
    public async Task ListForOwner_WalksThroughThePagesWithTheCursor_EvenWhenDatesAreEqual()
    {
        var owner = OwnerId.From(Guid.NewGuid());
        var createdAt = new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
        var stored = new List<Document>();
        foreach (var minutes in new[] { 0, 1, 1, 1, 2 })
        {
            stored.Add(await StoreAsync(owner, createdAt: createdAt.AddMinutes(minutes)));
        }

        var expected = stored
            .OrderByDescending(document => document.CreatedAt)
            .ThenByDescending(document => document.Id.Value)
            .Select(document => document.Id)
            .ToList();
        await using var context = CreateContext();
        var repository = new EfDocumentRepository(context);

        var seen = new List<DocumentId>();
        DocumentListCursor? cursor = null;
        var pages = 0;
        do
        {
            var page = await repository.ListForOwnerAsync(owner, Query(after: cursor, pageSize: 2), Token);
            seen.AddRange(page.Items.Select(summary => summary.Id));
            cursor = page.Next;
            pages++;
        }
        while (cursor is not null && pages < 10);

        seen.ShouldBe(expected);
        pages.ShouldBe(3);
    }

    [Theory]
    [InlineData("constitucion")]
    [InlineData("CONSTITUCIÓN")]
    [InlineData("española")]
    public async Task ListForOwner_FiltersByTitle_WithoutCaseOrAccents(string search)
    {
        var owner = OwnerId.From(Guid.NewGuid());
        var match = await StoreAsync(owner, title: "Constitución Española");
        await StoreAsync(owner, title: "Bases de la convocatoria");
        await using var context = CreateContext();

        var page = await new EfDocumentRepository(context).ListForOwnerAsync(owner, Query(title: search), Token);

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(match.Id);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    public async Task ListForOwner_TreatsLikeWildcardsInTheSearchAsPlainText(string search)
    {
        var owner = OwnerId.From(Guid.NewGuid());
        var match = await StoreAsync(owner, title: "Descuento del 50% en_tasas \\ especiales");
        await StoreAsync(owner, title: "Bases de la convocatoria");
        await using var context = CreateContext();

        var page = await new EfDocumentRepository(context).ListForOwnerAsync(owner, Query(title: search), Token);

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(match.Id);
    }

    [Fact]
    public async Task ListForOwner_FiltersByStatusAndTitleTogether()
    {
        var owner = OwnerId.From(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        var failedBases = await StoreAsync(owner, document => FailNow(document, now), title: "Bases de empleo");
        await StoreAsync(owner, title: "Bases de ayudas");
        await StoreAsync(owner, document => FailNow(document, now), title: "Convocatoria");
        await using var context = CreateContext();

        var page = await new EfDocumentRepository(context).ListForOwnerAsync(
            owner,
            Query(title: "bases", statuses: [DocumentStatus.Error, DocumentStatus.Validated]),
            Token);

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(failedBases.Id);
    }

    private static void FailNow(Document document, DateTimeOffset now)
    {
        document.StartProcessing(now);
        document.FailProcessing("llm.unavailable", now);
    }

    private static DocumentListQuery Query(
        string? title = null,
        DocumentStatus[]? statuses = null,
        DocumentListCursor? after = null,
        int pageSize = 10) =>
        new(title, new HashSet<DocumentStatus>(statuses ?? []), after, pageSize);

    private async Task<Document> StoreAsync(
        OwnerId owner,
        Action<Document>? prepare = null,
        DateTimeOffset? createdAt = null,
        string? title = null)
    {
        var document = Document.Submit(
            owner,
            title is null ? DocumentTitle.Default : DocumentTitle.Create(title),
            OriginalText.Create("Texto de prueba del documento.", Limits),
            createdAt ?? DateTimeOffset.UtcNow);
        prepare?.Invoke(document);
        await using var context = CreateContext();
        await new EfDocumentRepository(context).AddAsync(document, Token);
        return document;
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database.ConnectionString).Options);
}
