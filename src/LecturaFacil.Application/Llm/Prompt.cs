// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Llm;

/// <summary>Prompt versionado, por ejemplo <c>adapter.v1</c>. El nombre identifica la versión usada en cada llamada.</summary>
public sealed record Prompt(string Name, string Text);
