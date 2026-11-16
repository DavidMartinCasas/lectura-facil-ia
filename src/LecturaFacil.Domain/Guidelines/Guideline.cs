// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>Pauta de lectura fácil (inspirada en la UNE 153101 EX) que se comprueba con reglas deterministas.</summary>
public sealed record Guideline(string Code, string Description, Severity DefaultSeverity)
{
    /// <summary>Longitud máxima del código de una pauta.</summary>
    public const int CodeMaxLength = 64;
}
