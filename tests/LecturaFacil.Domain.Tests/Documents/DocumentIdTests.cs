// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Domain.Tests.Documents;

public sealed class DocumentIdTests
{
    [Fact]
    public void New_WhenCalled_ReturnsNonEmptyValue()
    {
        var id = DocumentId.New();

        id.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void New_WhenCalledTwice_ReturnsDifferentValues()
    {
        DocumentId.New().ShouldNotBe(DocumentId.New());
    }

    [Fact]
    public void From_WhenGuidIsEmpty_Throws()
    {
        Should.Throw<ArgumentException>(() => DocumentId.From(Guid.Empty));
    }

    [Fact]
    public void From_WhenGuidIsValid_KeepsValueAndEquality()
    {
        var guid = Guid.NewGuid();

        var id = DocumentId.From(guid);

        id.Value.ShouldBe(guid);
        id.ShouldBe(DocumentId.From(guid));
    }

    [Fact]
    public void ToString_WhenCalled_ReturnsGuidText()
    {
        var guid = Guid.NewGuid();

        DocumentId.From(guid).ToString().ShouldBe(guid.ToString());
    }
}
