// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Tests.Documents;

public sealed class ListDocumentsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = OwnerId.From(Guid.NewGuid());

    private readonly InMemoryDocumentRepository _repository = new();
    private readonly LimitsOptions _limits = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_WhenTheUserHasNoDocuments_ReturnsAnEmptyList()
    {
        var documents = await CreateUseCase().ExecuteAsync(Token);

        documents.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_ReturnsOnlyTheDocumentsOfTheCurrentUser_NewestFirst()
    {
        var older = Add(Owner, "Bases", Now.AddMinutes(-5));
        var newer = Add(Owner, "Convocatoria", Now);
        Add(OwnerId.From(Guid.NewGuid()), "De otra persona", Now.AddMinutes(1));

        var documents = await CreateUseCase().ExecuteAsync(Token);

        documents.Select(summary => summary.Id).ShouldBe([newer.Id, older.Id]);
        var first = documents[0];
        first.Title.ShouldBe("Convocatoria");
        first.Status.ShouldBe(DocumentStatus.Received);
        first.CreatedAt.ShouldBe(Now);
        first.Provider.ShouldBeNull();
    }

    [Fact]
    public async Task List_WhenTheDocumentIsAdapted_ReturnsTheProviderOfItsLatestVersion()
    {
        var document = Add(Owner, "Bases", Now);
        document.StartProcessing(Now);
        document.CompleteAdaptation(
            AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Qué es?", ["Una ayuda."])], [])!,
            new AdaptationMetadata("Servidor local", "modelo-secreto", "adapter.v1", 1, 1, TimeSpan.FromSeconds(1)),
            GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10),
            Now);

        var documents = await CreateUseCase().ExecuteAsync(Token);

        var summary = documents.ShouldHaveSingleItem();
        summary.Status.ShouldBe(DocumentStatus.Draft);
        summary.Provider.ShouldBe("Servidor local");
    }

    [Fact]
    public async Task List_ReturnsAtMostTheConfiguredNumberOfDocuments()
    {
        _limits.MaxDocumentsPerList = 2;
        Add(Owner, "Uno", Now.AddMinutes(-2));
        var second = Add(Owner, "Dos", Now.AddMinutes(-1));
        var third = Add(Owner, "Tres", Now);

        var documents = await CreateUseCase().ExecuteAsync(Token);

        documents.Select(summary => summary.Id).ShouldBe([third.Id, second.Id]);
    }

    private Document Add(OwnerId owner, string title, DateTimeOffset createdAt)
    {
        var document = Document.Submit(
            owner,
            DocumentTitle.Create(title),
            OriginalText.Create("Texto de prueba suficientemente largo.", new TextLengthLimits(10, 100)),
            createdAt);
        _repository.Added.Add(document);
        return document;
    }

    private ListDocuments CreateUseCase() => new(_repository, new FixedCurrentUser(Owner), Options.Create(_limits));
}
