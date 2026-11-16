// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>Palabra o expresión que conviene evitar y alternativa más sencilla (vacía si no hay una general).</summary>
public sealed record TermSuggestion(string Term, string Alternative);
