// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Domain.Tests.Documents;

public sealed class AdaptedContentPlainTextTests
{
    [Fact]
    public void PlainText_PutsTheTitleEachQuestionAndEachSentenceOnItsOwnLineWithoutTheGlossary()
    {
        var content = AdaptedContent.TryCreate(
            "Ayudas para el alquiler",
            [
                new AdaptedSectionDraft("¿Qué es?", ["Es una ayuda.", "La da el Ayuntamiento."]),
                new AdaptedSectionDraft("¿Quién la pide?", ["Usted."]),
            ],
            [new GlossaryTermDraft("Solicitud", "Papel para pedir algo.")])!;

        content.PlainText.ShouldBe("Ayudas para el alquiler\n¿Qué es?\nEs una ayuda.\nLa da el Ayuntamiento.\n¿Quién la pide?\nUsted.");
    }
}
