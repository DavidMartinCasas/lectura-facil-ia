// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Llm;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Evals.Dataset;
using LecturaFacil.Evals.DryRun;
using LecturaFacil.Evals.Running;
using LecturaFacil.Testing;

namespace LecturaFacil.Evals.Tests.Running;

public sealed class EvaluationPiecesTests
{
    private static readonly Prompt AdapterPrompt = new("adapter.v1", $"Adapta el texto entre <{LlmRequest.DocumentTagPlaceholder}>.");

    [Fact]
    public async Task DryRunResponder_WhenAdapting_ReturnsTheDocumentSentencesWithTheAdapterSchema()
    {
        var provider = new FakeLlmProvider(DryRunResponder.ProviderName, DryRunResponder.Respond);
        var request = LlmRequest.ForDocument(LlmRole.Adapter, AdapterPrompt, "Primera frase. Segunda frase", 100);

        var response = await provider.CompleteStructuredAsync<AdaptationReply>(request, TestContext.Current.CancellationToken);

        response.IsSuccess.ShouldBeTrue();
        response.Value.Model.ShouldBe(DryRunResponder.AdapterModel);
        response.Value.Value.Sections.Single().Sentences.ShouldBe(["Primera frase.", "Segunda frase."]);
        response.Value.InputTokens.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task MeteredLlmProvider_WhenResponsesSucceed_AddsTheirUsageUntilTaken()
    {
        var metered = new MeteredLlmProvider(new FakeLlmProvider("falso", _ => new LlmRecording("m", """{"titulo": "t", "secciones": [{"pregunta": "p", "frases": ["f"]}], "glosario": []}""", 10, 5, 1000)));
        var request = LlmRequest.ForDocument(LlmRole.Adapter, AdapterPrompt, "Texto.", 100);

        await metered.CompleteStructuredAsync<AdaptationReply>(request, TestContext.Current.CancellationToken);
        await metered.CompleteStructuredAsync<AdaptationReply>(request, TestContext.Current.CancellationToken);

        metered.Name.ShouldBe("falso");
        metered.Usage.ShouldBe(new LlmUsage(20, 10, TimeSpan.FromSeconds(2)));
        metered.TakeUsage().ShouldBe(new LlmUsage(20, 10, TimeSpan.FromSeconds(2)));
        metered.Usage.ShouldBe(new LlmUsage(0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task MeteredLlmProvider_WhenResponseFails_DoesNotCountIt()
    {
        var metered = new MeteredLlmProvider(FakeLlmProvider.Unavailable());
        var request = LlmRequest.ForDocument(LlmRole.Adapter, AdapterPrompt, "Texto.", 100);

        var response = await metered.CompleteStructuredAsync<AdaptationReply>(request, TestContext.Current.CancellationToken);

        response.IsFailure.ShouldBeTrue();
        metered.Usage.ShouldBe(new LlmUsage(0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ProfessionalAdaptation_WhenTextHasSeveralLines_UsesTheFirstAsTitleAndKeepsEveryLine()
    {
        var content = ProfessionalAdaptation.ToContent("Título\nPrimera frase.\n\nSegunda frase.");

        content.Title.ShouldBe("Título");
        content.Sections.Single().Sentences.ShouldBe(["Primera frase.", "Segunda frase."]);
        content.PlainText.ShouldBe("Título\nTítulo\nPrimera frase.\nSegunda frase.");
    }

    [Fact]
    public void ProfessionalAdaptation_WhenTextHasOneLine_UsesItAsTitleAndSentence()
    {
        ProfessionalAdaptation.ToContent("Solo una línea.").Sections.Single().Sentences.ShouldBe(["Solo una línea."]);
    }

    [Fact]
    public async Task InMemoryDocumentRepository_WhenOwnerDiffers_DoesNotReturnTheDocument()
    {
        var repository = new InMemoryDocumentRepository();
        var owner = OwnerId.From(Guid.NewGuid());
        var document = Document.Submit(owner, DocumentTitle.Default, OriginalText.Create(new string('a', 60), new TextLengthLimits(50, 1000)), DateTimeOffset.UnixEpoch);
        var token = TestContext.Current.CancellationToken;
        await repository.AddAsync(document, token);

        (await repository.FindForOwnerAsync(document.Id, owner, token)).ShouldBe(document);
        (await repository.FindForOwnerAsync(document.Id, OwnerId.From(Guid.NewGuid()), token)).ShouldBeNull();
        (await repository.FindStatusForOwnerAsync(document.Id, owner, token)).ShouldBe(DocumentStatus.Received);
        (await repository.ListAwaitingAdaptationAsync(token)).ShouldBe([document.Id]);
        await Should.ThrowAsync<NotSupportedException>(() => repository.ListForOwnerAsync(owner, new DocumentListQuery(null, new HashSet<DocumentStatus>(), null, 10), token));
    }
}
