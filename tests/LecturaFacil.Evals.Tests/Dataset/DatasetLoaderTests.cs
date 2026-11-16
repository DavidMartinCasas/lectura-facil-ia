// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Evals.Dataset;

namespace LecturaFacil.Evals.Tests.Dataset;

public sealed class DatasetLoaderTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("evals-dataset-");

    [Fact]
    public void Load_WhenFilesAreValid_ReturnsTheTextsOrderedById()
    {
        Write("b.json", Json("b-texto"));
        Write("a.json", Json("a-texto"));

        var result = DatasetLoader.Load(_directory.FullName);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(text => text.Id).ShouldBe(["a-texto", "b-texto"]);
        result.Value[0].ProfessionalAdaptation.ShouldBe("Tiene 15 días para pedirla.");
        result.Value[0].License.ShouldBe("CC0-1.0");
    }

    [Fact]
    public void Load_WhenDirectoryDoesNotExist_ReturnsNotFound()
    {
        var result = DatasetLoader.Load(Path.Combine(_directory.FullName, "no-existe"));

        result.Error.ShouldBe(DatasetErrors.NotFound);
    }

    [Fact]
    public void Load_WhenDirectoryHasNoTexts_ReturnsEmpty()
    {
        DatasetLoader.Load(_directory.FullName).Error.ShouldBe(DatasetErrors.Empty);
    }

    [Fact]
    public void Load_WhenThereAreTooManyTexts_ReturnsTooMany()
    {
        for (var i = 0; i <= DatasetLoader.MaxTexts; i++)
        {
            Write($"t{i}.json", Json($"t{i}"));
        }

        DatasetLoader.Load(_directory.FullName).Error!.Code.ShouldBe(DatasetErrors.TooManyCode);
    }

    [Fact]
    public void Load_WhenTwoTextsShareAnId_ReturnsDuplicate()
    {
        Write("a.json", Json("mismo"));
        Write("b.json", Json("mismo"));

        DatasetLoader.Load(_directory.FullName).Error!.Code.ShouldBe(DatasetErrors.DuplicateIdCode);
    }

    [Theory]
    [InlineData("""{"id": "x", "original": "Texto.", "adaptacion_profesional": "Texto.", "fuente": "", "licencia": "CC0-1.0"}""")]
    [InlineData("""{"id": "x", "original": "Texto.", "adaptacion_profesional": "Texto.", "fuente": "Fuente"}""")]
    [InlineData("""{"id": "x", "original": " ", "adaptacion_profesional": "Texto.", "fuente": "Fuente", "licencia": "CC0-1.0"}""")]
    [InlineData("""{"id": "../fuera", "original": "Texto.", "adaptacion_profesional": "Texto.", "fuente": "Fuente", "licencia": "CC0-1.0"}""")]
    [InlineData("""{"id": "X Mayúsculas", "original": "Texto.", "adaptacion_profesional": "Texto.", "fuente": "Fuente", "licencia": "CC0-1.0"}""")]
    [InlineData("no es json")]
    [InlineData("null")]
    public void Load_WhenATextIsIncomplete_ReturnsInvalid(string json)
    {
        Write("malo.json", json);

        var result = DatasetLoader.Load(_directory.FullName);

        result.Error!.Code.ShouldBe(DatasetErrors.InvalidCode);
        result.Error.Message.ShouldContain("malo.json");
    }

    [Fact]
    public void Load_WhenAFileIsTooLarge_ReturnsInvalid()
    {
        Write("grande.json", new string(' ', DatasetLoader.MaxFileBytes + 1));

        DatasetLoader.Load(_directory.FullName).Error!.Code.ShouldBe(DatasetErrors.InvalidCode);
    }

    [Fact]
    public void Load_WhenThereAreSubdirectories_OnlyReadsTopLevelFiles()
    {
        Write("a.json", Json("a"));
        Directory.CreateDirectory(Path.Combine(_directory.FullName, "ejemplos"));
        File.WriteAllText(Path.Combine(_directory.FullName, "ejemplos", "b.json"), Json("b"));

        DatasetLoader.Load(_directory.FullName).Value.Count.ShouldBe(1);
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private void Write(string name, string content) => File.WriteAllText(Path.Combine(_directory.FullName, name), content);

    private static string Json(string id) =>
        $$"""{"id": "{{id}}", "original": "El plazo es de quince días hábiles.", "adaptacion_profesional": "Tiene 15 días para pedirla.", "fuente": "Texto inventado", "licencia": "CC0-1.0"}""";
}
