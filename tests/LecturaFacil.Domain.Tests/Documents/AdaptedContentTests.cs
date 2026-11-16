// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Domain.Tests.Documents;

public sealed class AdaptedContentTests
{
    [Fact]
    public void TryCreate_WhenEverythingHasText_NormalizesAndKeepsTheOrder()
    {
        var content = AdaptedContent.TryCreate(
            " Ayudas  para el alquiler ",
            [new AdaptedSectionDraft("¿Quién puede pedirla?", ["Puede pedirla si vive de alquiler.", "  ", "No tiene otra casa."])],
            [new GlossaryTermDraft("Solicitud", "Papel que se rellena\u0007 para pedir algo.")]);

        content.ShouldNotBeNull();
        content.Title.ShouldBe("Ayudas para el alquiler");
        content.Sections.Count.ShouldBe(1);
        content.Sections[0].Question.ShouldBe("¿Quién puede pedirla?");
        content.Sections[0].Sentences.ShouldBe(["Puede pedirla si vive de alquiler.", "No tiene otra casa."]);
        content.Glossary.ShouldBe([new GlossaryTerm("Solicitud", "Papel que se rellena para pedir algo.")]);
    }

    [Fact]
    public void TryCreate_WhenTheTitleIsBlank_ReturnsNull() =>
        AdaptedContent.TryCreate(" ", [Section()], []).ShouldBeNull();

    [Fact]
    public void TryCreate_WhenThereAreNoSections_ReturnsNull() =>
        AdaptedContent.TryCreate("Título", [], []).ShouldBeNull();

    [Fact]
    public void TryCreate_WhenASectionHasNoQuestion_ReturnsNull() =>
        AdaptedContent.TryCreate("Título", [new AdaptedSectionDraft("\u0007", ["Frase."])], []).ShouldBeNull();

    [Fact]
    public void TryCreate_WhenASectionHasNoSentences_ReturnsNull() =>
        AdaptedContent.TryCreate("Título", [new AdaptedSectionDraft("¿Qué?", [" "])], []).ShouldBeNull();

    [Fact]
    public void TryCreate_WhenAGlossaryTermIsIncomplete_ReturnsNull() =>
        AdaptedContent.TryCreate("Título", [Section()], [new GlossaryTermDraft("Término", "")]).ShouldBeNull();

    [Fact]
    public void Merge_TakesTheFirstTitle_JoinsSectionsAndRemovesRepeatedTerms()
    {
        var first = AdaptedContent.TryCreate("Primero", [Section("¿Uno?")], [new GlossaryTermDraft("Solicitud", "Papel.")])!;
        var second = AdaptedContent.TryCreate("Segundo", [Section("¿Dos?")], [
            new GlossaryTermDraft("solicitud", "Otra definición."),
            new GlossaryTermDraft("Boletín", "Periódico oficial."),
        ])!;

        var merged = AdaptedContent.Merge([first, second]);

        merged.Title.ShouldBe("Primero");
        merged.Sections.Select(section => section.Question).ShouldBe(["¿Uno?", "¿Dos?"]);
        merged.Glossary.ShouldBe([new GlossaryTerm("Solicitud", "Papel."), new GlossaryTerm("Boletín", "Periódico oficial.")]);
    }

    [Fact]
    public void Merge_ComparesTermsWithSpanishRulesWhateverTheCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var first = AdaptedContent.TryCreate("T", [Section()], [new GlossaryTermDraft("Órgano", "Uno.")])!;
            var second = AdaptedContent.TryCreate("T", [Section()], [new GlossaryTermDraft("órgano", "Dos.")])!;

            AdaptedContent.Merge([first, second]).Glossary.ShouldHaveSingleItem().Definition.ShouldBe("Uno.");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Merge_WhenThereIsNothingToMerge_Throws() =>
        Should.Throw<ArgumentException>(() => AdaptedContent.Merge([]));

    private static AdaptedSectionDraft Section(string question = "¿Qué es?") => new(question, ["Es una ayuda."]);
}
