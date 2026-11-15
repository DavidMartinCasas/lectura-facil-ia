// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Documents;

/// <summary>Firma (magic bytes) de un PDF. La extensión y el Content-Type los decide el cliente: no son fiables.</summary>
public static class PdfSignature
{
    public static ReadOnlySpan<byte> Bytes => "%PDF-"u8;

    public static bool Matches(ReadOnlySpan<byte> content) => content.StartsWith(Bytes);
}
