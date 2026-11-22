// SPDX-License-Identifier: Apache-2.0
using System.Collections.Concurrent;
using LecturaFacil.Api.Tests.Security;
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Api.Tests.Documents;

/// <summary>API sin base de datos: los documentos se guardan en <see cref="Repository"/> para poder comprobarlos.</summary>
public sealed class DocumentsApiFactory(string environment = "Production") : WebApplicationFactory<Program>
{
    public RecordingDocumentRepository Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("Database:ConnectionString", TestConfiguration.UnreachableDatabase);
        foreach (var (key, value) in TestConfiguration.ValidLlmSettings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services => services
            .AddSingleton<IDocumentRepository>(Repository)
            .UseFakeLlm()
            .UseTestAuthentication()
            .RemoveAdaptationWorker());
    }
}

public sealed class RecordingDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentQueue<Document> _added = new();

    public IReadOnlyCollection<Document> Added => _added;

    public Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        _added.Enqueue(document);
        return Task.CompletedTask;
    }

    public Task<Document?> FindAsync(DocumentId id, CancellationToken cancellationToken) =>
        Task.FromResult(_added.FirstOrDefault(document => document.Id == id));

    public Task<Document?> FindForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(_added.FirstOrDefault(document => document.Id == id && document.OwnerId == ownerId));

    public Task<Document?> FindForOwnerToUpdateAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
        FindForOwnerAsync(id, ownerId, cancellationToken);

    public async Task<DocumentStatus?> FindStatusForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
        (await FindForOwnerAsync(id, ownerId, cancellationToken))?.Status;

    public async Task<DocumentProgress?> FindProgressForOwnerAsync(DocumentId id, OwnerId ownerId, CancellationToken cancellationToken) =>
        await FindForOwnerAsync(id, ownerId, cancellationToken) is { } document ? new DocumentProgress(document.Status, document.Stage) : null;

    public Task<IReadOnlyList<DocumentId>> ListAwaitingAdaptationAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DocumentId>>(
            [.. _added.Where(document => DocumentStatusTransitions.IsAwaitingAdaptation(document.Status)).Select(document => document.Id)]);

    public Task<DocumentPage> ListForOwnerAsync(OwnerId ownerId, DocumentListQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(DocumentPage.FromFetched(
            [.. _added.Where(document => document.OwnerId == ownerId && query.Matches(document))
                .OrderByDescending(document => document.CreatedAt)
                .ThenByDescending(document => document.Id.Value)
                .Take(query.PageSize + 1)
                .Select(DocumentSummary.From)],
            query.PageSize));

    public Task SaveAsync(Document document, CancellationToken cancellationToken) => Task.CompletedTask;
}
