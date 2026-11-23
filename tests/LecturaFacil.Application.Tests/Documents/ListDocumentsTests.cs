// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Readability;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;
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
    public async Task List_WhenTheUserHasNoDocuments_ReturnsAnEmptyPageWithoutNext()
    {
        var page = await ListAsync(new DocumentListRequest());

        page.Items.ShouldBeEmpty();
        page.Next.ShouldBeNull();
    }

    [Fact]
    public async Task List_ReturnsOnlyTheDocumentsOfTheCurrentUser_NewestFirst()
    {
        var older = Add(Owner, "Bases", Now.AddMinutes(-5));
        var newer = Add(Owner, "Convocatoria", Now);
        Add(OwnerId.From(Guid.NewGuid()), "De otra persona", Now.AddMinutes(1));

        var page = await ListAsync(new DocumentListRequest());

        page.Items.Select(summary => summary.Id).ShouldBe([newer.Id, older.Id]);
        var first = page.Items[0];
        first.Title.ShouldBe("Convocatoria");
        first.Status.ShouldBe(DocumentStatus.Received);
        first.CreatedAt.ShouldBe(Now);
        first.Provider.ShouldBeNull();
        first.WarningCount.ShouldBeNull();
        first.Readability.ShouldBeNull();
    }

    [Fact]
    public async Task List_WhenTheDocumentIsAdapted_ReturnsTheProviderOfItsLatestVersion()
    {
        var document = Add(Owner, "Bases", Now);
        Adapt(document);

        var page = await ListAsync(new DocumentListRequest());

        var summary = page.Items.ShouldHaveSingleItem();
        summary.Status.ShouldBe(DocumentStatus.Draft);
        summary.Provider.ShouldBe("Servidor local");
    }

    [Fact]
    public void Summary_OfAnAdaptedDocument_CarriesTheWarningsAndTheTextsOfItsLatestVersion()
    {
        var document = Add(Owner, "Bases", Now);
        Adapt(document);

        var summary = DocumentSummary.From(document);

        summary.WarningCount.ShouldBe(document.LatestAdaptedVersion!.ReviewWarningCount);
        summary.ReadabilityTexts.ShouldBe(new ReadabilityTexts("Texto de prueba suficientemente largo.", document.LatestAdaptedVersion.PlainText));
    }

    [Fact]
    public async Task List_WhenTheDocumentIsAdapted_ComparesTheReadabilityAndDropsTheTexts()
    {
        var document = Add(Owner, "Bases", Now);
        Adapt(document);
        var expected = new CompareReadability(new DomainReadabilityCalculator())
            .Compare(document.OriginalText.Value, document.LatestAdaptedVersion!.PlainText).Value;

        var page = await ListAsync(new DocumentListRequest());

        var summary = page.Items.ShouldHaveSingleItem();
        summary.Readability.ShouldBe(expected);
        summary.ReadabilityTexts.ShouldBeNull();
    }

    [Fact]
    public async Task List_WalksThroughAllThePages_WithoutRepeatingOrSkippingDocuments()
    {
        var added = Enumerable.Range(0, 5).Select(minute => Add(Owner, $"Documento {minute}", Now.AddMinutes(minute))).ToList();

        var first = await ListAsync(new DocumentListRequest(PageSize: 2));
        var second = await ListAsync(new DocumentListRequest(Cursor: first.Next!.Value.Encode(), PageSize: 2));
        var third = await ListAsync(new DocumentListRequest(Cursor: second.Next!.Value.Encode(), PageSize: 2));

        first.Items.Select(summary => summary.Id).ShouldBe([added[4].Id, added[3].Id]);
        second.Items.Select(summary => summary.Id).ShouldBe([added[2].Id, added[1].Id]);
        third.Items.Select(summary => summary.Id).ShouldBe([added[0].Id]);
        third.Next.ShouldBeNull();
    }

    [Fact]
    public async Task List_WhenThePageIsExactlyFull_DoesNotOfferAnotherPage()
    {
        Add(Owner, "Uno", Now);
        Add(Owner, "Dos", Now.AddMinutes(1));

        var page = await ListAsync(new DocumentListRequest(PageSize: 2));

        page.Items.Count.ShouldBe(2);
        page.Next.ShouldBeNull();
    }

    [Fact]
    public async Task List_WhenSeveralDocumentsShareTheSameDate_UsesTheIdToKeepTheirOrderAcrossPages()
    {
        var added = Enumerable.Range(0, 3).Select(index => Add(Owner, $"Documento {index}", Now)).ToList();

        var first = await ListAsync(new DocumentListRequest(PageSize: 2));
        var second = await ListAsync(new DocumentListRequest(Cursor: first.Next!.Value.Encode(), PageSize: 2));

        first.Items.Concat(second.Items).Select(summary => summary.Id)
            .ShouldBe(added.OrderByDescending(document => document.Id.Value).Select(document => document.Id));
    }

    [Theory]
    [InlineData("convocatoria")]
    [InlineData("CONVOCATORIA")]
    [InlineData("tecnico")]
    [InlineData("  Técnico   de  ")]
    public async Task List_FiltersByTitle_IgnoringCaseAccentsAndExtraSpaces(string search)
    {
        var match = Add(Owner, "Convocatoria de técnico de empleo", Now);
        Add(Owner, "Bases de ayudas", Now.AddMinutes(1));

        var page = await ListAsync(new DocumentListRequest(Title: search));

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(match.Id);
    }

    [Fact]
    public async Task List_FiltersByOneOrMoreStatuses()
    {
        var draft = Add(Owner, "Adaptado", Now);
        Adapt(draft);
        var received = Add(Owner, "Recibido", Now.AddMinutes(1));
        var failed = Add(Owner, "Con error", Now.AddMinutes(2));
        failed.StartProcessing(Now);
        failed.FailProcessing("llm.unavailable", Now);

        var onlyDrafts = await ListAsync(new DocumentListRequest(Statuses: ["Draft"]));
        var draftsAndErrors = await ListAsync(new DocumentListRequest(Statuses: ["Draft", "Error"]));

        onlyDrafts.Items.ShouldHaveSingleItem().Id.ShouldBe(draft.Id);
        draftsAndErrors.Items.Select(summary => summary.Id).ShouldBe([failed.Id, draft.Id]);
        draftsAndErrors.Items.ShouldNotContain(summary => summary.Id == received.Id);
    }

    [Fact]
    public async Task List_CombinesTitleAndStatus()
    {
        var match = Add(Owner, "Bases de ayudas", Now);
        Adapt(match);
        Add(Owner, "Bases de empleo", Now.AddMinutes(1));
        var otherTitle = Add(Owner, "Convocatoria", Now.AddMinutes(2));
        Adapt(otherTitle);

        var page = await ListAsync(new DocumentListRequest(Title: "bases", Statuses: ["Draft"]));

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(match.Id);
    }

    [Fact]
    public async Task List_WithoutPageSize_ReturnsTheDefaultPageSize()
    {
        for (var minute = 0; minute < ListDocuments.DefaultPageSize + 1; minute++)
        {
            Add(Owner, $"Documento {minute}", Now.AddMinutes(minute));
        }

        var page = await ListAsync(new DocumentListRequest());

        page.Items.Count.ShouldBe(ListDocuments.DefaultPageSize);
        page.Next.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task List_WhenThePageSizeIsOutOfRange_FailsWithInvalidPageSize(int pageSize)
    {
        var result = await CreateUseCase().ExecuteAsync(new DocumentListRequest(PageSize: pageSize), Token);

        result.Error!.Code.ShouldBe("documents.invalid_page_size");
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("3")]
    [InlineData("Draft,Error")]
    [InlineData("Archivado")]
    public async Task List_WhenAStatusIsNotAKnownName_FailsWithInvalidStatus(string status)
    {
        var result = await CreateUseCase().ExecuteAsync(new DocumentListRequest(Statuses: [status]), Token);

        result.Error.ShouldBe(DocumentListErrors.InvalidStatus);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-cursor")]
    [InlineData("MTIzNDU")]
    public async Task List_WhenTheCursorIsNotValid_FailsWithInvalidCursor(string cursor)
    {
        var result = await CreateUseCase().ExecuteAsync(new DocumentListRequest(Cursor: cursor), Token);

        result.Error.ShouldBe(DocumentListErrors.InvalidCursor);
    }

    [Fact]
    public async Task List_WhenTheSearchIsLongerThanATitle_FailsWithSearchTooLong()
    {
        var search = new string('a', DocumentTitle.MaxLength + 1);

        var result = await CreateUseCase().ExecuteAsync(new DocumentListRequest(Title: search), Token);

        result.Error.ShouldBe(DocumentListErrors.SearchTooLong);
    }

    [Fact]
    public void Cursor_RoundTripsThroughItsText()
    {
        var cursor = new DocumentListCursor(Now.AddTicks(123), Guid.CreateVersion7());

        DocumentListCursor.TryDecode(cursor.Encode(), out var decoded).ShouldBeTrue();

        decoded.ShouldBe(cursor);
    }

    private async Task<DocumentPage> ListAsync(DocumentListRequest request)
    {
        var result = await CreateUseCase().ExecuteAsync(request, Token);
        result.IsSuccess.ShouldBeTrue(result.Error?.Message);
        return result.Value;
    }

    private static void Adapt(Document document)
    {
        document.StartProcessing(Now);
        document.CompleteAdaptation(
            AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Qué es?", ["Una ayuda."])], [])!,
            new AdaptationMetadata("Servidor local", "modelo-secreto", "adapter.v1", 1, 1, TimeSpan.FromSeconds(1)),
            GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10),
            MeaningVerification.NotVerified("Texto de prueba.", 10),
            Now);
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

    private ListDocuments CreateUseCase() =>
        new(_repository, new FixedCurrentUser(Owner), Options.Create(_limits), new CompareReadability(new DomainReadabilityCalculator()));
}
