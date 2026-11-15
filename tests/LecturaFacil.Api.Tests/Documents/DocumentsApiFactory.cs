// SPDX-License-Identifier: Apache-2.0
using System.Collections.Concurrent;
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

        builder.ConfigureTestServices(services => services.AddSingleton<IDocumentRepository>(Repository));
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
}
