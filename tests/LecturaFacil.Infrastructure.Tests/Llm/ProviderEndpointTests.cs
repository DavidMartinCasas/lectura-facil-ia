// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Llm;

namespace LecturaFacil.Infrastructure.Tests.Llm;

public sealed class ProviderEndpointTests
{
    [Theory]
    [InlineData("https://proveedor.example.com/")]
    [InlineData("https://proveedor.example.com:8443/openai/v1")]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("http://127.0.0.1:8080/v1")]
    [InlineData("http://[::1]:8080/v1")]
    [InlineData("http://10.0.0.5:11434/v1")]
    [InlineData("http://172.16.1.1/v1")]
    [InlineData("http://172.31.255.255/v1")]
    [InlineData("http://192.168.1.10:1234/v1")]
    [InlineData("http://[fd00::1]:8080/v1")]
    public void IsAllowed_WhenHttpsOrHttpToLocalNetwork_ReturnsTrue(string endpoint)
    {
        ProviderEndpoint.IsAllowed(endpoint).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no es una url")]
    [InlineData("/v1/chat")]
    [InlineData("http://proveedor.example.com/v1")]
    [InlineData("http://ollama:11434/v1")]
    [InlineData("http://8.8.8.8/v1")]
    [InlineData("http://172.32.0.1/v1")]
    [InlineData("http://169.254.169.254/latest")]
    [InlineData("http://[2001:db8::1]/v1")]
    [InlineData("ftp://localhost/")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://usuario:clave@proveedor.example.com/")]
    public void IsAllowed_WhenNotHttpsOrPublicHttp_ReturnsFalse(string? endpoint)
    {
        ProviderEndpoint.IsAllowed(endpoint).ShouldBeFalse();
    }
}
