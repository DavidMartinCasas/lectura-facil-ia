// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Evals.Dataset;

public static class DatasetErrors
{
    public const string InvalidCode = "dataset.invalid";
    public const string TooManyCode = "dataset.too_many";
    public const string DuplicateIdCode = "dataset.duplicate_id";

    public static readonly Error NotFound = new("dataset.not_found", "No existe la carpeta del dataset.");

    public static readonly Error Empty = new("dataset.empty", "La carpeta del dataset no tiene ningún fichero .json.");

    public static Error TooMany(int max) => new(TooManyCode, $"El dataset no puede tener más de {max} textos.");

    public static Error DuplicateId(string id) => new(DuplicateIdCode, $"Hay dos textos con el identificador '{id}'.");

    public static Error Invalid(string fileName, string problem) => new(InvalidCode, $"{fileName}: {problem}");
}
