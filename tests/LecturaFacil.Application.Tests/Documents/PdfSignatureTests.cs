// SPDX-License-Identifier: Apache-2.0
using System.Text;
using LecturaFacil.Application.Documents;

namespace LecturaFacil.Application.Tests.Documents;

public sealed class PdfSignatureTests
{
    [Theory]
    [InlineData("%PDF-1.7\n", true)]
    [InlineData("%PDF-", true)]
    [InlineData("%PDF", false)]
    [InlineData("%pdf-1.7", false)]
    [InlineData(" %PDF-1.7", false)]
    [InlineData("PK\u0003\u0004", false)]
    [InlineData("", false)]
    public void Matches_WhenCalled_ChecksTheFirstBytes(string header, bool expected)
    {
        PdfSignature.Matches(Encoding.ASCII.GetBytes(header)).ShouldBe(expected);
    }
}
