// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;
using LecturaFacil.Application.Tests.Documents;
using LecturaFacil.Application.Verification;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Verification;
using LecturaFacil.Infrastructure.Verification;
using LecturaFacil.Testing;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Tests.Verification;

/// <summary>Verificación de un documento largo, una llamada por cada parte (US-23).</summary>
public sealed class VerifyMeaningPartsTests
{
    private readonly LimitsOptions _limits = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Verify_CallsTheVerifierOncePerPartWithOnlyThatPart()
    {
        var llm = Responding("verify/vacio", "verify/vacio", "verify/vacio");

        await CreateUseCase(llm).ExecuteAsync(
            [Part("Primera parte: 10 días.", "Tiene 10 días."), Part("Segunda parte: 20 €.", "Son 20 euros."), Part("Tercera parte.", "Fin.")],
            Merged("Tiene 10 días.", "Son 20 euros.", "Fin."),
            onPart: null,
            Token);

        var requests = llm.Requests.ToList();
        requests.Count.ShouldBe(3);
        requests[1].UserMessage.ShouldContain("Segunda parte: 20 €.");
        requests[1].UserMessage.ShouldContain("Son 20 euros.");
        requests[1].UserMessage.ShouldNotContain("Primera parte");
        requests[1].UserMessage.ShouldNotContain("Tiene 10 días.");
    }

    [Fact]
    public async Task Verify_JoinsTheKeyFactsOfEveryPart()
    {
        var llm = Responding("verify/conservado", "verify/cambiado");

        var verification = await CreateUseCase(llm).ExecuteAsync(
            [Part("Con un máximo de 250 €.", "Como mucho, 250 euros al mes."), Part("En el plazo de quince días hábiles.", "Tiene 10 días.")],
            Merged("Como mucho, 250 euros al mes.", "Tiene 10 días."),
            onPart: null,
            Token);

        verification.Status.ShouldBe(VerificationStatus.Verified);
        verification.KeyFacts.Select(fact => (fact.Type, fact.Status)).ShouldBe(
            [(KeyFactType.Deadline, KeyFactStatus.Review), (KeyFactType.Amount, KeyFactStatus.Conserved)]);
    }

    [Fact]
    public async Task Verify_WhenOnePartFails_KeepsTheOthersAndMarksThatPart()
    {
        var llm = Responding("verify/conservado", null, "verify/vacio");

        var verification = await CreateUseCase(llm).ExecuteAsync(
            [Part("Con un máximo de 250 €.", "Como mucho, 250 euros al mes."), Part("Plazo de 30 días.", "Tiene 30 días."), Part("Sin cifras.", "Nada.")],
            Merged("Como mucho, 250 euros al mes.", "Tiene 30 días.", "Nada."),
            onPart: null,
            Token);

        llm.Requests.Count.ShouldBe(3);
        verification.Status.ShouldBe(VerificationStatus.PartiallyVerified);
        verification.KeyFacts.ShouldContain(fact => fact.OriginalValue == "Parte 2" && fact.Reason == KeyFactReason.NotVerified);
        verification.KeyFacts.ShouldContain(fact => fact.OriginalValue == "máximo de 250 €" && fact.Status == KeyFactStatus.Conserved);
    }

    [Fact]
    public async Task Verify_AppliesTheLengthLimitToEachPart()
    {
        _limits.MaxCharactersPerVerification = 1_000;
        var llm = Responding("verify/vacio", "verify/vacio");

        var verification = await CreateUseCase(llm).ExecuteAsync(
            [Part($"Larga: 5 días. {new string('x', 1_000)}", "Tiene 5 días."), Part("Corta: 7 días.", "Tiene 7 días."), Part("Otra corta.", "Fin.")],
            Merged("Tiene 5 días.", "Tiene 7 días.", "Fin."),
            onPart: null,
            Token);

        // Solo la parte larga se queda sin verificar; las otras dos sí llegan al verificador.
        llm.Requests.Count.ShouldBe(2);
        verification.Status.ShouldBe(VerificationStatus.PartiallyVerified);
        verification.KeyFacts.ShouldContain(fact => fact.OriginalValue == "Parte 1");
    }

    [Fact]
    public async Task Verify_ReportsWhichPartItIsVerifying()
    {
        var progress = new List<(int Part, int Parts)>();

        await CreateUseCase(Responding("verify/vacio", "verify/vacio")).ExecuteAsync(
            [Part("Uno.", "Uno."), Part("Dos.", "Dos.")],
            Merged("Uno.", "Dos."),
            (part, parts, _) =>
            {
                progress.Add((part, parts));
                return Task.CompletedTask;
            },
            Token);

        progress.ShouldBe([(1, 2), (2, 2)]);
    }

    private VerifyMeaning CreateUseCase(ILlmProvider llm) =>
        new(new LlmMeaningVerifier(llm, Prompts.Real, Options.Create(_limits)), Options.Create(_limits));

    /// <summary>Responde a cada llamada con la grabación que toca; <c>null</c> es un proveedor caído.</summary>
    private static FakeLlmProvider Responding(params string?[] fixtures)
    {
        var calls = 0;
        return new FakeLlmProvider("Proveedor falso", _ =>
        {
            var fixture = fixtures[calls++];
            return fixture is null ? LlmRecording.Unavailable : LlmRecording.Load(fixture);
        });
    }

    private static PartToVerify Part(string original, params string[] sentences) => new(original, Adaptation(sentences));

    private static AdaptedContent Merged(params string[] sentences) => Adaptation(sentences);

    private static AdaptedContent Adaptation(params string[] sentences) =>
        AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft("¿Qué es?", sentences)], [])!;
}
