// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>Resultado de comprobar una pauta sobre un texto.</summary>
public enum GuidelineStatus
{
    Compliant,
    NotCompliant,

    /// <summary>La pauta no se ha podido comprobar (una búsqueda agotó su tiempo): no se puede dar por cumplida.</summary>
    NotChecked,
}
