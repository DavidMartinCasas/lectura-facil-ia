// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Llm;

/// <summary>Para qué se llama al modelo. Cada rol usa su propio modelo (o despliegue) configurado.</summary>
public enum LlmRole
{
    /// <summary>Adapta el texto a lectura fácil.</summary>
    Adapter,

    /// <summary>Verifica que la adaptación conserva el significado. Debe ser de otra familia de modelos.</summary>
    Verifier,
}
