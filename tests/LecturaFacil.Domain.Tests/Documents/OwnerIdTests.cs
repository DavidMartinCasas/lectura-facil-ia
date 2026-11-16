// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Domain.Tests.Documents;

public sealed class OwnerIdTests
{
    [Fact]
    public void From_WhenGuidIsEmpty_Throws()
    {
        Should.Throw<ArgumentException>(() => OwnerId.From(Guid.Empty));
    }

    [Fact]
    public void From_WhenGuidIsValid_KeepsValueAndEquality()
    {
        var guid = Guid.NewGuid();

        var owner = OwnerId.From(guid);

        owner.Value.ShouldBe(guid);
        owner.ShouldBe(OwnerId.From(guid));
        owner.ToString().ShouldBe(guid.ToString());
    }
}
