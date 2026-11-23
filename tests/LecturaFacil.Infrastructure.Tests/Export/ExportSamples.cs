// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Export;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Infrastructure.Tests.Export;

/// <summary>Documentos de ejemplo para los exportadores. Datos inventados, sin datos personales.</summary>
internal static class ExportSamples
{
    public const string Footer = "Borrador · pendiente de validación · 16 de noviembre de 2026";

    public static readonly ExportCover Cover = new("Adaptación a lectura fácil", "Borrador · pendiente de validación", "16 de noviembre de 2026");

    public static ExportableDocument Rent() => new(
        "Ayudas al alquiler",
        [
            new AdaptedSection("¿Qué es esta ayuda?", ["Es una ayuda de 250 € al mes.", "Sirve para pagar el alquiler de tu casa."]),
            new AdaptedSection("¿Cómo la pido?", ["Rellena la solicitud.", "Entrégala antes del 31 de diciembre."]),
        ],
        "Glosario",
        [new GlossaryTerm("Alquiler", "Dinero que pagas cada mes por vivir en una casa.")],
        Footer,
        "es",
        Cover);

    public static ExportableDocument WithText(string sentence, string? title = null) => Rent() with
    {
        Title = title ?? "Ayudas al alquiler",
        Sections = [new AdaptedSection("¿Qué es?", [sentence])],
    };
}
