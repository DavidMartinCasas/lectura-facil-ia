// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Api.Documents;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Api.Tests.Documents;

/// <summary>TestServer no aplica los límites de Kestrel: se comprueba que salen de LimitsOptions.</summary>
public sealed class RequestLimitsTests
{
    [Fact]
    public async Task Limits_WhenMaxFileSizeIsConfigured_AreAppliedToKestrelAndForms()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = await factory.CreateApiClientAsync();
        const long expected = (5 * 1024 * 1024) + RequestLimits.MultipartOverheadBytes;

        var kestrel = factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        var form = factory.Services.GetRequiredService<IOptions<FormOptions>>().Value;

        kestrel.Limits.MaxRequestBodySize.ShouldBe(expected);
        form.MultipartBodyLengthLimit.ShouldBe(expected);
        form.MemoryBufferThreshold.ShouldBe((int)expected);
    }
}
