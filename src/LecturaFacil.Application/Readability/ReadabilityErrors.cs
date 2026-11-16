// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Readability;

/// <summary>Errores esperados al medir la legibilidad. Los mensajes se muestran al usuario.</summary>
public static class ReadabilityErrors
{
    public static readonly Error NoWords = new(
        "readability.no_words",
        "No se puede calcular la legibilidad de un texto sin palabras.");
}
