// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Adaptation;

/// <summary>
/// Cómo ha ido la adaptación de un documento. A propósito no tiene ningún campo de texto del documento ni de la
/// respuesta del modelo: solo identificadores, proveedor, modelo, tokens, duración y un código de resultado.
/// </summary>
/// <param name="ProviderRole">Quién respondió la última vez, o <c>null</c> si ningún modelo dio una respuesta válida.</param>
/// <param name="InputTokens">Tokens de todas las respuestas válidas, también las de intentos descartados.</param>
/// <param name="Result"><see cref="SuccessResult"/> o el código del error.</param>
/// <param name="TokensByRole">
/// Los mismos tokens, separados por el proveedor que los consumió: si se pasa del principal al respaldo a mitad de
/// adaptación, cada uno cuenta los suyos. Vacío si ningún modelo dio una respuesta válida.
/// </param>
public sealed record AdaptationOutcome(
    DocumentId DocumentId,
    LlmProviderRole? ProviderRole,
    string? Provider,
    string? Model,
    long InputTokens,
    long OutputTokens,
    TimeSpan Duration,
    string Result,
    IReadOnlyList<RoleTokens>? TokensByRole = null)
{
    public const string SuccessResult = "ok";

    /// <summary>Código para un fallo inesperado (una excepción), que no llega como <see cref="Error"/>.</summary>
    public const string UnexpectedResult = "unexpected";

    public bool IsSuccess => Result == SuccessResult;

    public IReadOnlyList<RoleTokens> Tokens => TokensByRole ?? [];

    public static AdaptationOutcome Unexpected(DocumentId documentId, TimeSpan duration) =>
        new(documentId, null, null, null, 0, 0, duration, UnexpectedResult);
}

/// <summary>Tokens consumidos por un proveedor durante una adaptación.</summary>
public sealed record RoleTokens(LlmProviderRole Role, long InputTokens, long OutputTokens);
