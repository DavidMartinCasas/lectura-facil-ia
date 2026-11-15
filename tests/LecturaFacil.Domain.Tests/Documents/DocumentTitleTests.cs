// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Domain.Tests.Documents;

public sealed class DocumentTitleTests
{
    [Fact]
    public void Create_WhenValueIsNormal_KeepsIt()
    {
        DocumentTitle.Create("Convocatoria de ayudas").Value.ShouldBe("Convocatoria de ayudas");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0000\u0007\t")]
    public void Create_WhenValueIsEmptyAfterSanitizing_UsesTheDefaultTitle(string? value)
    {
        DocumentTitle.Create(value).Value.ShouldBe(DocumentTitle.Default.Value);
    }

    [Fact]
    public void Create_WhenValueHasControlCharactersAndRepeatedSpaces_RemovesThem()
    {
        DocumentTitle.Create("  Bases\u0000 de la\r\n\tconvocatoria\u001B  ").Value.ShouldBe("Bases de la convocatoria");
    }

    [Fact]
    public void Create_WhenValueIsTooLong_TruncatesItToTheMaximumLength()
    {
        var title = DocumentTitle.Create(new string('a', DocumentTitle.MaxLength + 50));

        title.Value.Length.ShouldBe(DocumentTitle.MaxLength);
    }

    [Fact]
    public void Create_WhenTruncationWouldSplitASurrogatePair_DoesNotLeaveHalfACharacter()
    {
        var value = new string('a', DocumentTitle.MaxLength - 1) + "😀";

        var title = DocumentTitle.Create(value);

        title.Value.ShouldBe(new string('a', DocumentTitle.MaxLength - 1));
    }

    [Fact]
    public void Create_WhenValueIsDecomposed_NormalizesToNfc()
    {
        DocumentTitle.Create("Resolucio\u0301n").Value.ShouldBe("Resolución");
    }

    [Theory]
    [InlineData("documento.pdf", "documento")]
    [InlineData("Bases ayudas 2026.PDF", "Bases ayudas 2026")]
    [InlineData("../../etc/passwd.pdf", "passwd")]
    [InlineData("C:\\Users\\ana\\Escritorio\\bases.pdf", "bases")]
    [InlineData("informe", "informe")]
    public void FromFileName_WhenCalled_KeepsOnlyTheNameWithoutPathOrExtension(string fileName, string expected)
    {
        DocumentTitle.FromFileName(fileName).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(".pdf")]
    [InlineData("/")]
    public void FromFileName_WhenThereIsNoUsableName_UsesTheDefaultTitle(string? fileName)
    {
        DocumentTitle.FromFileName(fileName).ShouldBe(DocumentTitle.Default);
    }
}
