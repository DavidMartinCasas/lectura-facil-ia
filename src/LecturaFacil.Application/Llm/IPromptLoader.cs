// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Llm;

/// <summary>Carga los prompts versionados. Los prompts nunca se escriben como cadenas en el código.</summary>
public interface IPromptLoader
{
    /// <param name="name">Nombre y versión, por ejemplo <c>adapter.v1</c>.</param>
    /// <exception cref="InvalidOperationException">Si el prompt no existe: es un error de programación.</exception>
    Prompt Load(string name);
}
