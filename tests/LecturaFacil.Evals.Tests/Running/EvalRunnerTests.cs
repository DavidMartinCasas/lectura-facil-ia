// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Evals.Reporting;
using LecturaFacil.Evals.Running;

namespace LecturaFacil.Evals.Tests.Running;

/// <summary>Ejecución completa en <c>--dry-run</c> con los textos de ejemplo: nunca llama a un modelo real.</summary>
public sealed class EvalRunnerTests : IDisposable
{
    private static readonly string Dataset = Path.Combine(AppContext.BaseDirectory, "datasets");

    private readonly DirectoryInfo _output = Directory.CreateTempSubdirectory("evals-resultados-");
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero));
    private readonly StringWriter _console = new();

    [Fact]
    public async Task RunAsync_WhenDryRun_ProcessesEveryTextWithEveryModelAndWritesTheReports()
    {
        var exitCode = await Run("--dry-run", "--models", "gemma,qwen,azure-mini", "--dataset", Dataset, "--output", _output.FullName);

        exitCode.ShouldBe(EvalRunner.Success, _console.ToString());
        var directory = Path.Combine(_output.FullName, "2026-11-23-dry-run");
        var report = await File.ReadAllTextAsync(Path.Combine(directory, EvalRunner.ReportFileName), TestContext.Current.CancellationToken);
        var csv = await File.ReadAllLinesAsync(Path.Combine(directory, EvalRunner.CsvFileName), TestContext.Current.CancellationToken);

        csv[0].ShouldBe(CsvReport.Header);
        csv.Count(line => line.Contains(",text,", StringComparison.Ordinal)).ShouldBe(6);
        csv.Count(line => line.Contains(",trap,", StringComparison.Ordinal)).ShouldBe(6);
        csv.Where(line => line.Contains(",text,", StringComparison.Ordinal)).ShouldAllBe(line => line.Contains(",\"ok\",", StringComparison.Ordinal));
        report.ShouldContain("Simulación (`--dry-run`)");
        report.ShouldContain("| gemma | 2 de 2 |");
        report.ShouldContain("| azure-mini | 2 de 2 |");
    }

    [Fact]
    public async Task RunAsync_WhenDryRun_EvaluatesOneTrapPerExampleAndReportsTheRecall()
    {
        await Run("--dry-run", "--dataset", Dataset, "--output", _output.FullName);

        var directory = Path.Combine(_output.FullName, "2026-11-23-dry-run");
        var csv = await File.ReadAllLinesAsync(Path.Combine(directory, EvalRunner.CsvFileName), TestContext.Current.CancellationToken);
        var report = await File.ReadAllTextAsync(Path.Combine(directory, EvalRunner.ReportFileName), TestContext.Current.CancellationToken);
        var traps = csv.Where(line => line.Contains(",trap,", StringComparison.Ordinal)).ToList();
        traps.Count.ShouldBe(2);
        // "40 de cada 100" → "41 de cada 100": el 40 del original desaparece y la comprobación de cifras por código lo avisa sin verificador.
        traps.ShouldContain(line => line.Contains("\"ayuda-alquiler\"", StringComparison.Ordinal) && line.EndsWith(",40,41,true", StringComparison.Ordinal));
        report.ShouldContain("| simulado | ayuda-alquiler | 40 → 41 | Sí |");
        report.ShouldContain("de 2) |");
    }

    [Fact]
    public async Task RunAsync_WhenDryRun_RecordsPromptVersionsModelsAndParameters()
    {
        await Run("--dry-run", "--dataset", Dataset, "--output", _output.FullName);

        var report = await File.ReadAllTextAsync(
            Path.Combine(_output.FullName, "2026-11-23-dry-run", EvalRunner.ReportFileName), TestContext.Current.CancellationToken);
        report.ShouldContain("| adapter.v1 | `");
        report.ShouldContain("| verify.v1 | `");
        report.ShouldContain("MaxOutputTokens = ");
        report.ShouldContain("adaptador-simulado");
        report.ShouldContain("verificador-simulado");
        report.ShouldContain("| ayuda-alquiler | Texto inventado");
    }

    [Fact]
    public async Task RunAsync_WhenArgumentsAreInvalid_ReturnsInvalidInputAndShowsUsage()
    {
        var exitCode = await Run("--desconocida");

        exitCode.ShouldBe(EvalRunner.InvalidInput);
        _console.ToString().ShouldContain("Uso:");
    }

    [Fact]
    public async Task RunAsync_WhenHelpIsAsked_ShowsUsage()
    {
        (await Run("--help")).ShouldBe(EvalRunner.Success);
        _console.ToString().ShouldContain("--dry-run");
    }

    [Fact]
    public async Task RunAsync_WhenDatasetDoesNotExist_ReturnsInvalidInput()
    {
        (await Run("--dry-run", "--dataset", Path.Combine(_output.FullName, "no-existe"))).ShouldBe(EvalRunner.InvalidInput);
    }

    [Fact]
    public async Task RunAsync_WhenPaidModelIsNotConfirmed_CancelsWithoutCallingAnyModel()
    {
        var config = WritePaidConfig();

        var exitCode = await Run(new StringReader("n\n"), "--models", "nube", "--dataset", Dataset, "--output", _output.FullName, "--config", config);

        exitCode.ShouldBe(EvalRunner.Cancelled);
        _console.ToString().ShouldContain("coste estimado");
        _output.GetDirectories().ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenInputEndsWithoutAnswer_Cancels()
    {
        var config = WritePaidConfig();

        var exitCode = await Run(new StringReader(string.Empty), "--models", "nube", "--dataset", Dataset, "--output", _output.FullName, "--config", config);

        exitCode.ShouldBe(EvalRunner.Cancelled);
    }

    [Fact]
    public async Task RunAsync_WhenConfigIsInvalid_ReturnsInvalidInput()
    {
        var exitCode = await Run("--models", "nube", "--dataset", Dataset, "--config", Path.Combine(_output.FullName, "no-existe.json"));

        exitCode.ShouldBe(EvalRunner.InvalidInput);
    }

    public void Dispose()
    {
        _console.Dispose();
        _output.Delete(recursive: true);
    }

    private Task<int> Run(params string[] args) => Run(new StringReader(string.Empty), args);

    private Task<int> Run(TextReader input, params string[] args)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NUBE_ENDPOINT"] = "https://recurso.invalid/",
            ["NUBE_KEY"] = "clave-de-prueba",
        };
        var runner = new EvalRunner(input, _console, _time, environment.GetValueOrDefault);
        return runner.RunAsync(args, TestContext.Current.CancellationToken);
    }

    private string WritePaidConfig()
    {
        var path = Path.Combine(_output.FullName, "modelos.json");
        File.WriteAllText(path, """
            {
              "models": {
                "nube": {
                  "type": "AzureFoundry", "displayName": "Nube", "endpointVariable": "NUBE_ENDPOINT", "apiKeyVariable": "NUBE_KEY",
                  "adapterModel": "mini", "verifierModel": "otro", "inputPricePerMillion": 0.44, "outputPricePerMillion": 1.76
                }
              }
            }
            """);
        return path;
    }
}

/// <summary>Fecha fija para saber en qué carpeta se escriben los resultados; el reloj de intervalos es el real.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
