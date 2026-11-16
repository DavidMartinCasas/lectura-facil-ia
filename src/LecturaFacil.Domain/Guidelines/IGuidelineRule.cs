// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>
/// Regla determinista que comprueba una pauta. Añadir una pauta es añadir una clase que implementa esta interfaz:
/// el <see cref="GuidelineAnalyzer"/> no cambia.
/// </summary>
public interface IGuidelineRule
{
    Guideline Guideline { get; }

    IEnumerable<Finding> Check(GuidelineText text);
}
