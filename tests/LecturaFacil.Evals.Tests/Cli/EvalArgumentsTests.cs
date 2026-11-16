// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Evals.Cli;

namespace LecturaFacil.Evals.Tests.Cli;

public sealed class EvalArgumentsTests
{
    [Fact]
    public void Parse_WhenModelsAreGiven_SplitsThemAndUsesTheDefaultPaths()
    {
        var result = EvalArguments.Parse(["--models", "gemma,qwen,azure-mini"]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Models.ShouldBe(["gemma", "qwen", "azure-mini"]);
        result.Value.DryRun.ShouldBeFalse();
        result.Value.Yes.ShouldBeFalse();
        result.Value.DatasetDirectory.ShouldBe(EvalArguments.DefaultDatasetDirectory);
        result.Value.OutputDirectory.ShouldBe(EvalArguments.DefaultOutputDirectory);
        result.Value.ConfigPath.ShouldBe(EvalArguments.DefaultConfigPath);
    }

    [Fact]
    public void Parse_WhenDryRunWithoutModels_UsesTheSimulatedModelAndTheExampleDataset()
    {
        var result = EvalArguments.Parse(["--dry-run"]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DryRun.ShouldBeTrue();
        result.Value.Models.ShouldBe([EvalArguments.DryRunModel]);
        result.Value.DatasetDirectory.ShouldBe(EvalArguments.DryRunDatasetDirectory);
    }

    [Fact]
    public void Parse_WhenAllOptionsAreGiven_ReadsThem()
    {
        var result = EvalArguments.Parse(
            ["--models", "gemma", "--yes", "--dataset", "otra/carpeta", "--output", "salida", "--config", "mis-modelos.json"]);

        result.Value.Yes.ShouldBeTrue();
        result.Value.DatasetDirectory.ShouldBe("otra/carpeta");
        result.Value.OutputDirectory.ShouldBe("salida");
        result.Value.ConfigPath.ShouldBe("mis-modelos.json");
    }

    [Fact]
    public void Parse_WhenHelpIsAsked_ReturnsHelp()
    {
        EvalArguments.Parse(["--help"]).Value.Help.ShouldBeTrue();
    }

    [Theory]
    [InlineData]
    [InlineData("--yes")]
    public void Parse_WhenModelsAreMissingWithoutDryRun_Fails(params string[] args)
    {
        EvalArguments.Parse(args).Error!.Code.ShouldBe(EvalArguments.InvalidCode);
    }

    [Theory]
    [InlineData("--models")]
    [InlineData("--models", "")]
    [InlineData("--models", "Gemma")]
    [InlineData("--models", "gemma,,qwen")]
    [InlineData("--models", "gemma,gemma")]
    [InlineData("--models", "a,b,c,d,e,f")]
    [InlineData("--models", "../fuera")]
    [InlineData("--models", "gemma", "--dataset")]
    [InlineData("--models", "gemma", "--desconocida")]
    [InlineData("--models", "gemma", "--models", "qwen")]
    public void Parse_WhenArgumentsAreInvalid_Fails(params string[] args)
    {
        EvalArguments.Parse(args).Error!.Code.ShouldBe(EvalArguments.InvalidCode);
    }
}
