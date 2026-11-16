// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>Números escritos con letras. No incluye "un", "uno" ni "una" porque casi siempre son artículos.</summary>
internal static class SpanishNumberWords
{
    public const string Alternation =
        "cero|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|once|doce|trece|catorce|quince|"
        + "dieciséis|dieciseis|diecisiete|dieciocho|diecinueve|veinte|veintiuno|veintiuna|veintidós|veintidos|"
        + "veintitrés|veintitres|veinticuatro|veinticinco|veintiséis|veintiseis|veintisiete|veintiocho|veintinueve|"
        + "treinta|cuarenta|cincuenta|sesenta|setenta|ochenta|noventa|cien|ciento|doscientos|doscientas|"
        + "trescientos|trescientas|cuatrocientos|cuatrocientas|quinientos|quinientas|seiscientos|seiscientas|"
        + "setecientos|setecientas|ochocientos|ochocientas|novecientos|novecientas|mil|millón|millones";
}
