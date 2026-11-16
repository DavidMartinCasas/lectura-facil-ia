// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;
using LecturaFacil.Application.Common;

namespace LecturaFacil.Evals.Cli;

/// <summary>Opciones de la línea de comandos. Las rutas por defecto son relativas a la raíz del repositorio.</summary>
public sealed partial record EvalArguments(
    IReadOnlyList<string> Models,
    bool DryRun,
    bool Yes,
    string DatasetDirectory,
    string OutputDirectory,
    string ConfigPath,
    bool Help = false)
{
    public const string InvalidCode = "arguments.invalid";
    public const string DefaultDatasetDirectory = "evals/datasets";
    public const string DryRunDatasetDirectory = "evals/datasets/ejemplos";
    public const string DefaultOutputDirectory = "evals/resultados";
    public const string DefaultConfigPath = "evals/modelos.json";

    /// <summary>Nombre del modelo simulado cuando se usa <c>--dry-run</c> sin <c>--models</c>.</summary>
    public const string DryRunModel = "simulado";

    /// <summary>El alcance son 3 modelos; se deja algo de margen.</summary>
    public const int MaxModels = 5;

    public const string Usage = """
        Uso: dotnet run --project evals/LecturaFacil.Evals -- [opciones]

          --models a,b,c   Perfiles de evals/modelos.json que se evalúan (obligatorio salvo con --dry-run).
          --dry-run        Usa el proveedor falso: no llama a ningún modelo ni necesita claves.
          --yes            No pide confirmación del coste estimado antes de usar modelos de pago.
          --dataset ruta   Carpeta con los textos (por defecto evals/datasets; con --dry-run, evals/datasets/ejemplos).
          --output ruta    Carpeta de resultados (por defecto evals/resultados).
          --config ruta    Fichero de perfiles de modelos (por defecto evals/modelos.json).
          --help           Muestra esta ayuda.
        """;

    public static Result<EvalArguments> Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i];
            if (name is "--dry-run" or "--yes" or "--help")
            {
                flags.Add(name);
            }
            else if (name is not ("--models" or "--dataset" or "--output" or "--config"))
            {
                return Invalid($"Opción desconocida: {name}.");
            }
            else if (i + 1 >= args.Count || values.ContainsKey(name))
            {
                return Invalid($"La opción {name} necesita un valor y solo puede aparecer una vez.");
            }
            else
            {
                values[name] = args[++i];
            }
        }

        if (flags.Contains("--help"))
        {
            return Result<EvalArguments>.Success(new EvalArguments([], false, false, string.Empty, string.Empty, string.Empty, Help: true));
        }

        var dryRun = flags.Contains("--dry-run");
        var models = ParseModels(values.GetValueOrDefault("--models"), dryRun);
        if (models.IsFailure)
        {
            return Result<EvalArguments>.Failure(models.Error!);
        }

        return Result<EvalArguments>.Success(new EvalArguments(
            models.Value,
            dryRun,
            flags.Contains("--yes"),
            values.GetValueOrDefault("--dataset") ?? (dryRun ? DryRunDatasetDirectory : DefaultDatasetDirectory),
            values.GetValueOrDefault("--output") ?? DefaultOutputDirectory,
            values.GetValueOrDefault("--config") ?? DefaultConfigPath));
    }

    private static Result<IReadOnlyList<string>> ParseModels(string? value, bool dryRun)
    {
        if (value is null)
        {
            return dryRun
                ? Result<IReadOnlyList<string>>.Success([DryRunModel])
                : Result<IReadOnlyList<string>>.Failure(InvalidError("Indique los modelos con --models (o use --dry-run)."));
        }

        var models = value.Split(',');
        if (models.Length > MaxModels)
        {
            return Result<IReadOnlyList<string>>.Failure(InvalidError($"Como mucho se evalúan {MaxModels} modelos a la vez."));
        }

        if (!models.All(ModelNamePattern().IsMatch) || models.Distinct(StringComparer.Ordinal).Count() != models.Length)
        {
            return Result<IReadOnlyList<string>>.Failure(
                InvalidError("Los nombres de modelo solo admiten minúsculas, dígitos y guiones, sin repetirse."));
        }

        return Result<IReadOnlyList<string>>.Success(models);
    }

    private static Result<EvalArguments> Invalid(string message) => Result<EvalArguments>.Failure(InvalidError(message));

    private static Error InvalidError(string message) => new(InvalidCode, message);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,39}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex ModelNamePattern();
}
