// SPDX-License-Identifier: Apache-2.0
using System.Net;
using LecturaFacil.Api.Observability;
using Microsoft.AspNetCore.Http;

namespace LecturaFacil.Api.Tests.Observability;

public sealed class InternalNetworkTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.18.0.5")]
    [InlineData("192.168.1.10")]
    [InlineData("100.100.1.1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:10.0.0.1")]
    public void IsInternal_ForLoopbackAndPrivateAddresses_IsTrue(string address) =>
        InternalNetwork.IsInternal(IPAddress.Parse(address)).ShouldBeTrue();

    [Theory]
    [InlineData("203.0.113.7")]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("2001:db8::1")]
    [InlineData("::ffff:203.0.113.7")]
    public void IsInternal_ForPublicAddresses_IsFalse(string address) =>
        InternalNetwork.IsInternal(IPAddress.Parse(address)).ShouldBeFalse();

    [Theory]
    [InlineData("X-Forwarded-For")]
    [InlineData("Forwarded")]
    [InlineData("X-Real-IP")]
    public void IsInternal_WhenTheRequestCameThroughAProxy_IsFalseEvenFromAPrivateAddress(string header)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("172.18.0.3");
        context.Request.Headers[header] = "203.0.113.7";

        InternalNetwork.IsInternal(context).ShouldBeFalse();
    }

    [Fact]
    public void IsInternal_WhenTheRemoteAddressIsUnknown_IsFalse() =>
        InternalNetwork.IsInternal(new DefaultHttpContext()).ShouldBeFalse();
}
