// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Review;

/// <summary>Errores esperados al revisar un borrador: editarlo o marcar sus avisos. Los mensajes se muestran al usuario.</summary>
public static class ReviewErrors
{
    public const string EditTooLongCode = "review.edit_too_long";

    public static readonly Error NotEditable = new(
        "review.not_editable",
        "Solo se puede modificar un documento que está en borrador.");

    public static readonly Error VersionConflict = new(
        "review.version_conflict",
        "El documento ha cambiado mientras lo revisabas. Vuelve a cargarlo para ver la última versión.");

    public static readonly Error IncompleteEdit = new(
        "review.incomplete_edit",
        "La adaptación necesita un título y, en cada sección, una pregunta y al menos una frase. Cada palabra del glosario necesita su explicación.");

    public static readonly Error KeyFactNotFound = new(
        "review.key_fact_not_found",
        "No se ha encontrado ese dato clave.");

    public static Error EditTooLong(int maxCharacters) => new(
        EditTooLongCode,
        string.Create(CultureInfo.InvariantCulture, $"La adaptación no puede superar {maxCharacters} caracteres."));
}
