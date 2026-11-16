// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Evals.Dataset;
using LecturaFacil.Evals.Traps;

namespace LecturaFacil.Evals.Tests.Traps;

public sealed class TrapGeneratorTests
{
    [Fact]
    public void Create_WhenAdaptationHasAFigure_ChangesOnlyTheFirstOne()
    {
        var text = Text("plazo", "Tiene 15 días para pedir la ayuda.\nComo mucho, 250 euros al mes.");

        var trap = TrapGenerator.Create(text);

        trap.ShouldNotBeNull();
        trap.OriginalFigure.ShouldBe(15);
        trap.AlteredFigure.ShouldBe(16);
        trap.AlteredAdaptation.ShouldBe("Tiene 16 días para pedir la ayuda.\nComo mucho, 250 euros al mes.");
        trap.Source.ShouldBe(text);
    }

    [Fact]
    public void Create_WhenFigureHasDecimalsOrThousands_SkipsItAndUsesTheNextWholeNumber()
    {
        var text = Text("decimales", "Gana menos de 2,5 veces el IPREM.\nPaga 1.200 euros.\nTiene 10 días.");

        var trap = TrapGenerator.Create(text);

        trap.ShouldNotBeNull();
        trap.AlteredAdaptation.ShouldBe("Gana menos de 2,5 veces el IPREM.\nPaga 1.200 euros.\nTiene 11 días.");
    }

    [Fact]
    public void Create_WhenNumberHasALeadingZero_SkipsIt()
    {
        var text = Text("telefono", "Llame al 010.\nLlegue 10 minutos antes.");

        TrapGenerator.Create(text)!.AlteredAdaptation.ShouldBe("Llame al 010.\nLlegue 11 minutos antes.");
    }

    [Fact]
    public void Create_WhenAdaptationHasNoWholeNumber_ReturnsNull()
    {
        TrapGenerator.Create(Text("sin-cifras", "No hay ninguna cifra en este texto.")).ShouldBeNull();
    }

    [Fact]
    public void Create_WhenCalledTwice_ReturnsTheSameTrap()
    {
        var text = Text("estable", "Tiene 15 días.");

        TrapGenerator.Create(text).ShouldBe(TrapGenerator.Create(text));
    }

    [Fact]
    public void Generate_WhenThereAreMoreCandidatesThanTheMaximum_TakesTheFirstOnesInOrder()
    {
        var texts = new[]
        {
            Text("a", "Tiene 1 día."),
            Text("b", "Sin cifras."),
            Text("c", "Tiene 3 días."),
            Text("d", "Tiene 4 días."),
        };

        var traps = TrapGenerator.Generate(texts, maxTraps: 2);

        traps.Select(trap => trap.Source.Id).ShouldBe(["a", "c"]);
    }

    [Fact]
    public void Generate_WhenMaximumIsNegative_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => TrapGenerator.Generate([], maxTraps: -1));
    }

    private static EvalText Text(string id, string adaptation) =>
        new(id, "Texto original de prueba con quince días.", adaptation, "Texto inventado", "CC0-1.0");
}
