// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Review;
using LecturaFacil.Application.Tests.Documents;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Readability;
using LecturaFacil.Domain.Verification;
using LecturaFacil.Infrastructure.Guidelines;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Tests.Review;

public sealed class SaveManualEditTests
{
    private const string Original =
        "La ayuda ascenderá al 40 % de la renta mensual, con un máximo de 250 €. Las solicitudes se presentan en quince días.";

    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = OwnerId.From(Guid.NewGuid());

    private readonly InMemoryDocumentRepository _repository = new();
    private readonly LimitsOptions _limits = new();
    private readonly DomainReadabilityCalculator _readability = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveManualEdit_CreatesANewVersionWithItsGuidelinesAndReadabilityRecalculated()
    {
        var document = Adapted(Owner);
        var sentences = new[] { "Le damos como mucho 250 euros.", "Tiene 15 días para pedirla.", "Paga el 40 % del alquiler." };

        var result = await CreateUseCase().ExecuteAsync(Command(document, 1, sentences), Token);

        var details = result.Value;
        var version = details.Adaptation.ShouldNotBeNull();
        version.Number.ShouldBe(2);
        version.Sections.ShouldHaveSingleItem().Sentences.ShouldBe(sentences);
        version.Metadata.PromptVersion.ShouldBe(SaveManualEdit.ManualEditPromptVersion);
        version.Metadata.Provider.ShouldBe("Servidor propio");
        version.Metadata.InputTokens.ShouldBe(0);
        version.GuidelineReport!.Find(GuidelineCodes.Percentages).Status.ShouldBe(GuidelineStatus.NotCompliant);
        details.Readability!.Adapted.ShouldBe(SzigrisztPazosCalculator.Calculate(version.PlainText));
        details.Status.ShouldBe(DocumentStatus.Draft);
        document.AdaptedVersions.Count.ShouldBe(2);
        _repository.SavedStatuses.ShouldBe([DocumentStatus.Draft]);
    }

    [Fact]
    public async Task SaveManualEdit_RechecksTheKeyFactsWithCode_AndWarnsAboutAFigureThatDisappears()
    {
        var document = Adapted(Owner);

        var result = await CreateUseCase().ExecuteAsync(Command(document, 1, ["Le damos como mucho 250 euros.", "Paga el 40 % del alquiler."]), Token);

        var verification = result.Value.Adaptation!.MeaningVerification.ShouldNotBeNull();
        var added = verification.KeyFacts.Single(fact => fact.Type == KeyFactType.Figure);
        added.OriginalValue.ShouldBe("quince");
        added.RequiresAcknowledgement.ShouldBeTrue();
        verification.Status.ShouldBe(VerificationStatus.Verified);
    }

    [Fact]
    public async Task SaveManualEdit_KeepsTheAcknowledgedWarningsThatDoNotChange()
    {
        var document = Adapted(Owner);
        document.AcknowledgeKeyFact(0, Now);

        var result = await CreateUseCase().ExecuteAsync(
            Command(document, 1, ["Le damos como mucho 250 euros.", "Tiene 15 días para pedirla."]),
            Token);

        result.Value.Adaptation!.PendingKeyFactCount.ShouldBe(0);
    }

    [Fact]
    public async Task SaveManualEdit_WhenTheVersionIsNotTheLatest_ReturnsAConflictAndSavesNothing()
    {
        var document = Adapted(Owner);
        await CreateUseCase().ExecuteAsync(Command(document, 1, ["Primera edición."]), Token);

        var result = await CreateUseCase().ExecuteAsync(Command(document, 1, ["Segunda edición."]), Token);

        result.Error.ShouldBe(ReviewErrors.VersionConflict);
        document.AdaptedVersions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SaveManualEdit_WhenAnotherRequestChangedTheDocument_ReturnsAConflict()
    {
        var document = Adapted(Owner);
        _repository.FailNextSaveWithConflict = true;

        var result = await CreateUseCase().ExecuteAsync(Command(document, 1, ["Una frase."]), Token);

        result.Error.ShouldBe(ReviewErrors.VersionConflict);
    }

    [Fact]
    public async Task SaveManualEdit_WhenTheDocumentIsNotADraft_ReturnsNotEditable()
    {
        var document = Submit(Owner);

        var result = await CreateUseCase().ExecuteAsync(Command(document, 1, ["Una frase."]), Token);

        result.Error.ShouldBe(ReviewErrors.NotEditable);
        _repository.SavedStatuses.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveManualEdit_WhenTheDocumentBelongsToSomeoneElse_ReturnsNotFound()
    {
        var document = Adapted(OwnerId.From(Guid.NewGuid()));

        var result = await CreateUseCase().ExecuteAsync(Command(document, 1, ["Una frase."]), Token);

        result.Error.ShouldBe(DocumentErrors.NotFound);
        document.AdaptedVersions.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("", "¿Qué es?", "Una frase.")]
    [InlineData("Ayudas", " ", "Una frase.")]
    [InlineData("Ayudas", "¿Qué es?", "  ")]
    public async Task SaveManualEdit_WhenSomethingRequiredIsEmpty_ReturnsIncompleteEdit(string title, string question, string sentence)
    {
        var document = Adapted(Owner);

        var result = await CreateUseCase().ExecuteAsync(
            new SaveManualEditCommand(document.Id, 1, title, [new AdaptedSectionDraft(question, [sentence])], []),
            Token);

        result.Error.ShouldBe(ReviewErrors.IncompleteEdit);
        document.AdaptedVersions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SaveManualEdit_WhenTheEditIsTooLong_ReturnsEditTooLong()
    {
        _limits.MaxEditedCharacters = 1_000;
        var document = Adapted(Owner);

        var result = await CreateUseCase().ExecuteAsync(Command(document, 1, [new string('a', 1_001)]), Token);

        result.Error!.Code.ShouldBe(ReviewErrors.EditTooLongCode);
        document.AdaptedVersions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SaveManualEdit_NormalizesTheTextLikeAnAdaptation()
    {
        var document = Adapted(Owner);

        var result = await CreateUseCase().ExecuteAsync(Command(document, 1, ["  Una\tfrase\ncon espacios. ", ""]), Token);

        result.Value.Adaptation!.Sections[0].Sentences.ShouldBe(["Una frase con espacios."]);
    }

    private static SaveManualEditCommand Command(Document document, int version, IReadOnlyList<string> sentences) =>
        new(document.Id, version, "Ayudas para el alquiler", [new AdaptedSectionDraft("¿Qué es esta ayuda?", sentences)], []);

    private Document Adapted(OwnerId owner)
    {
        var document = Submit(owner);
        document.StartProcessing(Now);
        var content = AdaptedContent.TryCreate(
            "Ayudas para el alquiler",
            [new AdaptedSectionDraft("¿Qué es esta ayuda?", ["Le damos como mucho 250 euros.", "Tiene 15 días para pedirla."])],
            [])!;
        document.CompleteAdaptation(
            content,
            new AdaptationMetadata("Servidor propio", "modelo", "adapter.v1", 10, 20, TimeSpan.FromSeconds(1)),
            GuidelineReport.Create(new GuidelineAnalysis([], []), [], 10),
            MeaningVerification.Restore(
                VerificationStatus.Verified,
                [
                    KeyFact.Create(KeyFactType.Amount, "40 % de la renta mensual", null, new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation)),
                    KeyFact.Create(KeyFactType.Amount, "máximo de 250 €", "como mucho 250 euros", KeyFactVerdict.Conserved),
                ],
                new VerificationMetadata("Verificador", "modelo-verificador", "verify.v1")),
            Now);
        return document;
    }

    private Document Submit(OwnerId owner)
    {
        var document = Document.Submit(owner, DocumentTitle.Create("Bases"), OriginalText.Create(Original, new TextLengthLimits(10, 1_000)), Now);
        _repository.Added.Add(document);
        return document;
    }

    private SaveManualEdit CreateUseCase() =>
        new(
            _repository,
            new FixedCurrentUser(Owner),
            new ReviewAdaptation(new GuidelineChecker(Options.Create(_limits), NullLogger<GuidelineChecker>.Instance), Options.Create(_limits)),
            DomainReadabilityCalculator.Describe(_readability),
            Options.Create(_limits),
            new FixedTimeProvider(Now));
}
