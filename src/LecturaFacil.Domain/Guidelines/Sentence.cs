// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>Frase de un texto: <c>text[Start..End] == Text</c>, sin espacios al principio ni al final.</summary>
public sealed record Sentence(int Start, int End, string Text);
