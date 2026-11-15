// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Api.Documents;

/// <summary>Texto pegado. El título es opcional.</summary>
public sealed record SubmitTextRequest(string? Text, string? Title);

/// <summary>Documento recibido: su identificador y su estado (<c>Received</c>).</summary>
public sealed record SubmitDocumentResponse(Guid Id, string Status);

/// <summary>Campo <c>file</c> de un <c>multipart/form-data</c>: el PDF que se sube.</summary>
public sealed record SubmitPdfForm(IFormFile? File)
{
    public const string FieldName = "file";
}
