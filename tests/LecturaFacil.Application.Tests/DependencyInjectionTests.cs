// SPDX-License-Identifier: Apache-2.0
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_WhenCalled_ReturnsSameCollection()
    {
        var services = new ServiceCollection();

        services.AddApplication().ShouldBeSameAs(services);
    }

    [Fact]
    public void AddApplication_WhenServicesIsNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => DependencyInjection.AddApplication(null!));
    }
}
