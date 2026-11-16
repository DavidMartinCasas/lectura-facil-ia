// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.RegularExpressions;
using LecturaFacil.Application.Common;

namespace LecturaFacil.Evals.Dataset;

/// <summary>
/// Lee los textos del dataset: un fichero JSON por texto en la carpeta indicada (sin entrar en subcarpetas), con
/// <c>id</c>, <c>original</c>, <c>adaptacion_profesional</c>, <c>fuente</c> y <c>licencia</c>. Si un fichero no es
/// válido, no se evalúa nada: un dataset a medias daría resultados engañosos.
/// </summary>
public static partial class DatasetLoader
{
    /// <summary>El alcance son 10 textos, ampliable a 20 (sección 18 del documento de diseño).</summary>
    public const int MaxTexts = 20;

    public const int MaxFileBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new() { AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };

    public static Result<IReadOnlyList<EvalText>> Load(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory))
        {
            return Result<IReadOnlyList<EvalText>>.Failure(DatasetErrors.NotFound);
        }

        var files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal).ToList();
        if (files.Count == 0)
        {
            return Result<IReadOnlyList<EvalText>>.Failure(DatasetErrors.Empty);
        }

        if (files.Count > MaxTexts)
        {
            return Result<IReadOnlyList<EvalText>>.Failure(DatasetErrors.TooMany(MaxTexts));
        }

        var texts = new List<EvalText>();
        foreach (var file in files)
        {
            var text = Read(file);
            if (text.IsFailure)
            {
                return Result<IReadOnlyList<EvalText>>.Failure(text.Error!);
            }

            texts.Add(text.Value);
        }

        var duplicate = texts.GroupBy(text => text.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        return duplicate is null
            ? Result<IReadOnlyList<EvalText>>.Success(texts.OrderBy(text => text.Id, StringComparer.Ordinal).ToList().AsReadOnly())
            : Result<IReadOnlyList<EvalText>>.Failure(DatasetErrors.DuplicateId(duplicate.Key));
    }

    private static Result<EvalText> Read(string path)
    {
        var fileName = Path.GetFileName(path);
        if (new FileInfo(path).Length > MaxFileBytes)
        {
            return Result<EvalText>.Failure(DatasetErrors.Invalid(fileName, $"el fichero supera {MaxFileBytes} bytes."));
        }

        try
        {
            var text = JsonSerializer.Deserialize<EvalText>(File.ReadAllText(path), JsonOptions);
            return Validate(text) is { } problem
                ? Result<EvalText>.Failure(DatasetErrors.Invalid(fileName, problem))
                : Result<EvalText>.Success(text!);
        }
        catch (JsonException)
        {
            return Result<EvalText>.Failure(DatasetErrors.Invalid(fileName, "no es un JSON válido."));
        }
    }

    private static string? Validate(EvalText? text)
    {
        if (text is null)
        {
            return "está vacío.";
        }

        if (text.Id is null || !IdPattern().IsMatch(text.Id))
        {
            return "'id' es obligatorio y solo admite minúsculas, dígitos y guiones (hasta 64 caracteres).";
        }

        var missing = new[]
            {
                ("original", text.Original),
                ("adaptacion_profesional", text.ProfessionalAdaptation),
                ("fuente", text.Source),
                ("licencia", text.License),
            }
            .Where(field => string.IsNullOrWhiteSpace(field.Item2))
            .Select(field => field.Item1)
            .ToList();
        return missing.Count == 0 ? null : $"faltan campos obligatorios: {string.Join(", ", missing)}.";
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex IdPattern();
}
