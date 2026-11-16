// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Llm;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Application.Tests.Documents;

public sealed class GetDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = OwnerId.From(Guid.NewGuid());

    private readonly InMemoryDocumentRepository _repository = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_WhenTheDocumentIsReceived_ReturnsItsStatusWithoutAdaptation()
    {
        var document = Add(Owner);

        var result = await CreateUseCase().ExecuteAsync(document.Id, Token);

        result.Value.Id.ShouldBe(document.Id);
        result.Value.Title.ShouldBe("Bases");
        result.Value.Status.ShouldBe(DocumentStatus.Received);
        result.Value.Adaptation.ShouldBeNull();
        result.Value.Failure.ShouldBeNull();
    }

    [Fact]
    public async Task Get_WhenTheDocumentIsInDraft_ReturnsTheLatestAdaptation()
    {
        var document = Add(Owner);
        document.StartProcessing(Now);
        var version = document.CompleteAdaptation(
            AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Qué es?", ["Una ayuda."])], [])!,
            new AdaptationMetadata("Proveedor", "modelo", "adapter.v1", 1, 1, TimeSpan.FromSeconds(1)),
            GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10),
            Now);

        var result = await CreateUseCase().ExecuteAsync(document.Id, Token);

        result.Value.Status.ShouldBe(DocumentStatus.Draft);
        result.Value.Adaptation.ShouldBe(version);
    }

    [Theory]
    [InlineData("llm.provider_unavailable")]
    [InlineData("llm.invalid_provider_output")]
    [InlineData("algo.desconocido")]
    public async Task Get_WhenTheAdaptationFailed_ReturnsAGenericReason(string failureCode)
    {
        var document = Add(Owner);
        document.StartProcessing(Now);
        document.FailProcessing(failureCode, Now);

        var result = await CreateUseCase().ExecuteAsync(document.Id, Token);

        var failure = result.Value.Failure.ShouldNotBeNull();
        failure.ShouldBe(AdaptationErrors.FromFailureCode(failureCode));
        failure.Message.ShouldNotContain("modelo", Case.Insensitive);
    }

    [Fact]
    public void FromFailureCode_KnownCodesKeepTheirMessage_AndUnknownOnesAreGeneric()
    {
        AdaptationErrors.FromFailureCode(LlmErrors.ProviderUnavailable.Code).ShouldBe(LlmErrors.ProviderUnavailable);
        AdaptationErrors.FromFailureCode("algo.desconocido").ShouldBe(AdaptationErrors.Failed);
    }

    [Fact]
    public async Task Get_WhenTheDocumentBelongsToSomeoneElse_ReturnsNotFound()
    {
        var document = Add(OwnerId.From(Guid.NewGuid()));

        var result = await CreateUseCase().ExecuteAsync(document.Id, Token);

        result.Error.ShouldBe(DocumentErrors.NotFound);
    }

    [Fact]
    public async Task Get_WhenTheDocumentDoesNotExist_ReturnsNotFound()
    {
        var result = await CreateUseCase().ExecuteAsync(DocumentId.New(), Token);

        result.Error.ShouldBe(DocumentErrors.NotFound);
    }

    private Document Add(OwnerId owner)
    {
        var document = Document.Submit(
            owner,
            DocumentTitle.Create("Bases"),
            OriginalText.Create("Texto de prueba suficientemente largo.", new TextLengthLimits(10, 100)),
            Now);
        _repository.Added.Add(document);
        return document;
    }

    private GetDocument CreateUseCase() => new(_repository, new FixedCurrentUser(Owner));
}
