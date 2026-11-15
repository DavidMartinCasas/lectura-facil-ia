// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Documents;

/// <summary>Texto pegado por el usuario. El título es opcional.</summary>
public sealed record SubmitTextCommand(string Text, string? Title);

/// <summary>PDF subido por el usuario. El contenido se procesa en memoria y nunca se guarda en disco.</summary>
public sealed record SubmitPdfCommand(Stream Content, string? FileName);

/// <param name="PageCount">Número de páginas si el documento llegó como PDF.</param>
public sealed record SubmittedDocument(DocumentId Id, DocumentStatus Status, int Characters, int? PageCount);
