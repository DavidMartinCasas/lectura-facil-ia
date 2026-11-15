// SPDX-License-Identifier: Apache-2.0
using System.Reflection;
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Tests.Common;

public sealed class SecretAttributeTests
{
    [Fact]
    public void IsSecret_WhenPropertyIsMarked_ReturnsTrue()
    {
        SecretAttribute.IsSecret(typeof(Sample).GetProperty(nameof(Sample.Key))!).ShouldBeTrue();
    }

    [Fact]
    public void IsSecret_WhenPropertyIsNotMarked_ReturnsFalse()
    {
        SecretAttribute.IsSecret(typeof(Sample).GetProperty(nameof(Sample.Name))!).ShouldBeFalse();
    }

    [Fact]
    public void IsSecret_WhenPropertyIsNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => SecretAttribute.IsSecret((PropertyInfo)null!));
    }

    private sealed class Sample
    {
        [Secret]
        public string Key { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;
    }
}
