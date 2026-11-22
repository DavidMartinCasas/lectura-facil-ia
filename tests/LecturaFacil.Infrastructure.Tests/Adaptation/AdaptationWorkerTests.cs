// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application;
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Llm;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Infrastructure.Adaptation;
using LecturaFacil.Infrastructure.Persistence;
using LecturaFacil.Infrastructure.Tests.Persistence;
using LecturaFacil.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Tests.Adaptation;

/// <summary>El worker contra PostgreSQL real (Testcontainers) y <see cref="FakeLlmProvider"/>.</summary>
public sealed class AdaptationWorkerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private const string Text = "La Concejalía de Vivienda convoca ayudas para el alquiler de la vivienda habitual.";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly RecordingStatuses _statuses = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(Token);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Worker_TakesAQueuedDocumentFromReceivedThroughProcessingToDraft()
    {
        var document = await StoreAsync(processing: false);
        await using var services = BuildServices(FakeLlmProvider.FromFixture("adapter/ok"));
        var worker = await StartWorkerAsync(services);

        await services.GetRequiredService<IAdaptationQueue>().EnqueueAsync(document.Id, Token);
        var stored = await WaitUntilFinishedAsync(document.Id);
        await worker.StopAsync(Token);

        _statuses.For(document.Id).ShouldBe([DocumentStatus.Processing, DocumentStatus.Draft]);
        stored.Status.ShouldBe(DocumentStatus.Draft);
        var version = stored.LatestAdaptedVersion.ShouldNotBeNull();
        version.Number.ShouldBe(1);
        version.Title.ShouldBe("Ayudas para pagar el alquiler");
        version.Sections.Count.ShouldBe(4);
        version.Sections[3].Sentences.ShouldContain("Tiene 15 días para pedirla.");
        version.Glossary.Count.ShouldBe(3);
        version.Metadata.ShouldBe(new AdaptationMetadata(
            "Proveedor falso", "modelo-adaptador-falso", AdaptDocument.PromptName, 1200, 900, TimeSpan.FromMilliseconds(3400)));
    }

    [Fact]
    public async Task Worker_OnStartup_RequeuesTheDocumentsThatWereProcessingWhenTheServiceStopped()
    {
        var document = await StoreAsync(processing: true);
        await using var services = BuildServices(FakeLlmProvider.FromFixture("adapter/ok"));

        var worker = await StartWorkerAsync(services);
        var stored = await WaitUntilFinishedAsync(document.Id);
        await worker.StopAsync(Token);

        stored.Status.ShouldBe(DocumentStatus.Draft);
        stored.AdaptedVersions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Worker_WhenTheModelKeepsAnsweringBadly_LeavesTheDocumentInErrorAndKeepsWorking()
    {
        var failing = await StoreAsync(processing: false);
        var calls = 0;
        var llm = new FakeLlmProvider("Proveedor falso", _ =>
            LlmRecording.Load(Interlocked.Increment(ref calls) <= 2 ? "adapter/poema" : "adapter/ok"));
        await using var services = BuildServices(llm);
        var worker = await StartWorkerAsync(services);
        var queue = services.GetRequiredService<IAdaptationQueue>();

        await queue.EnqueueAsync(failing.Id, Token);
        var failed = await WaitUntilFinishedAsync(failing.Id);
        var next = await StoreAsync(processing: false);
        await queue.EnqueueAsync(next.Id, Token);
        var adapted = await WaitUntilFinishedAsync(next.Id);
        await worker.StopAsync(Token);

        failed.Status.ShouldBe(DocumentStatus.Error);
        failed.FailureCode.ShouldBe(LlmErrors.InvalidProviderOutput.Code);
        adapted.Status.ShouldBe(DocumentStatus.Draft);
    }

    [Fact]
    public async Task Worker_WhenAdaptingThrowsUnexpectedly_LeavesTheDocumentInErrorInsteadOfProcessing()
    {
        var document = await StoreAsync(processing: false);
        var llm = new FakeLlmProvider("Proveedor falso", _ => throw new InvalidOperationException("Fallo inesperado"));
        await using var services = BuildServices(llm);
        var worker = await StartWorkerAsync(services);

        await services.GetRequiredService<IAdaptationQueue>().EnqueueAsync(document.Id, Token);
        var stored = await WaitUntilFinishedAsync(document.Id);
        await worker.StopAsync(Token);

        stored.Status.ShouldBe(DocumentStatus.Error);
        stored.FailureCode.ShouldBe(AdaptationErrors.Failed.Code);
    }

    private async Task<Document> StoreAsync(bool processing)
    {
        var document = Document.Submit(
            OwnerId.From(Guid.NewGuid()),
            DocumentTitle.Create("Bases"),
            OriginalText.Create(Text, new TextLengthLimits(10, 1_000)),
            DateTimeOffset.UtcNow);
        if (processing)
        {
            document.StartProcessing(DateTimeOffset.UtcNow);
        }

        await using var context = CreateContext();
        await new EfDocumentRepository(context).AddAsync(document, Token);
        return document;
    }

    private async Task<Document> WaitUntilFinishedAsync(DocumentId id)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(Timeout);
        while (true)
        {
            await using var context = CreateContext();
            var document = await context.Set<Document>().AsNoTracking().SingleAsync(saved => saved.Id == id, timeout.Token);
            if (!DocumentStatusTransitions.IsAwaitingAdaptation(document.Status))
            {
                return document;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    private static async Task<AdaptationWorker> StartWorkerAsync(ServiceProvider services)
    {
        var worker = services.GetServices<IHostedService>().OfType<AdaptationWorker>().Single();
        await worker.StartAsync(Token);
        return worker;
    }

    /// <summary>La composición real de Application e Infrastructure, con la base de datos de prueba y el proveedor falso.</summary>
    private ServiceProvider BuildServices(ILlmProvider llm)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(new DatabaseOptions { ConnectionString = database.ConnectionString }));
        services.AddSingleton(Options.Create(new LimitsOptions()));
        services.AddApplication().AddInfrastructure();
        services.AddSingleton<ICurrentUser>(new UnusedCurrentUser());
        services.AddTransient(_ => llm);
        services.AddScoped<EfDocumentRepository>();
        services.AddScoped<IDocumentRepository>(provider =>
            new StatusRecordingRepository(provider.GetRequiredService<EfDocumentRepository>(), _statuses));
        return services.BuildServiceProvider();
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database.ConnectionString).Options);

    private sealed class UnusedCurrentUser : ICurrentUser
    {
        public OwnerId OwnerId => throw new InvalidOperationException("The worker never acts as a user.");
    }

    private sealed class RecordingStatuses
    {
        private readonly List<(DocumentId Id, DocumentStatus Status)> _saved = [];

        public void Add(DocumentId id, DocumentStatus status)
        {
            lock (_saved)
            {
                _saved.Add((id, status));
            }
        }

        public List<DocumentStatus> For(DocumentId id)
        {
            lock (_saved)
            {
                return [.. _saved.Where(saved => saved.Id == id).Select(saved => saved.Status)];
            }
        }
    }

    /// <summary>Anota cada estado guardado para comprobar el recorrido completo, no solo el final.</summary>
    private sealed class StatusRecordingRepository(EfDocumentRepository inner, RecordingStatuses statuses) : IDocumentRepository
    {
        public Task AddAsync(Document document, CancellationToken cancellationToken) => inner.AddAsync(document, cancellationToken);

        public Task<Document?> FindAsync(DocumentId id, CancellationToken cancellationToken) => inner.FindAsync(id, cancellationToken);

        public Task<Document?> FindForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
            inner.FindForOwnerAsync(id, ownerId, cancellationToken);

        public Task<Document?> FindForOwnerToUpdateAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
            inner.FindForOwnerToUpdateAsync(id, ownerId, cancellationToken);

        public Task<DocumentStatus?> FindStatusForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
            inner.FindStatusForOwnerAsync(id, ownerId, cancellationToken);

        public Task<IReadOnlyList<DocumentId>> ListAwaitingAdaptationAsync(CancellationToken cancellationToken) =>
            inner.ListAwaitingAdaptationAsync(cancellationToken);

        public Task<DocumentPage> ListForOwnerAsync(OwnerId ownerId, DocumentListQuery query, CancellationToken cancellationToken) =>
            inner.ListForOwnerAsync(ownerId, query, cancellationToken);

        public async Task SaveAsync(Document document, CancellationToken cancellationToken)
        {
            await inner.SaveAsync(document, cancellationToken);
            statuses.Add(document.Id, document.Status);
        }
    }
}
