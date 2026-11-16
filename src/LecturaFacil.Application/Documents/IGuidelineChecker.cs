// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Application.Documents;

/// <summary>Comprueba las pautas de lectura fácil de un texto con reglas deterministas (sin LLM).</summary>
public interface IGuidelineChecker
{
    /// <summary>Las posiciones de los hallazgos se refieren a <paramref name="text"/> tal y como se recibe.</summary>
    GuidelineAnalysis Check(string text);
}
