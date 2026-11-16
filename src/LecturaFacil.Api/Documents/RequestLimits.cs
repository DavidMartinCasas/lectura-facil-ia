// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Api.Documents;

/// <summary>
/// Tamaño máximo de petición, derivado de <see cref="LimitsOptions.MaxFileSizeBytes"/>, aplicado en Kestrel y en la
/// lectura de formularios. Los formularios se quedan en memoria: el fichero subido nunca se escribe en disco.
/// </summary>
internal static class RequestLimits
{
    /// <summary>Margen para las cabeceras y separadores de <c>multipart/form-data</c> alrededor del fichero.</summary>
    public const int MultipartOverheadBytes = 64 * 1024;

    /// <summary>El formulario solo lleva el fichero: unos pocos campos de sobra bastan.</summary>
    public const int MaxFormFields = 16;

    public static long MaxRequestBytes(LimitsOptions limits) => (long)limits.MaxFileSizeBytes + MultipartOverheadBytes;

    public static IServiceCollection AddRequestLimits(this IServiceCollection services)
    {
        services.AddOptions<KestrelServerOptions>()
            .Configure<IOptions<LimitsOptions>>((kestrel, limits) =>
                kestrel.Limits.MaxRequestBodySize = MaxRequestBytes(limits.Value));

        services.AddOptions<FormOptions>()
            .Configure<IOptions<LimitsOptions>>((form, limits) =>
            {
                var maxBytes = (int)MaxRequestBytes(limits.Value);
                form.MultipartBodyLengthLimit = maxBytes;
                form.MemoryBufferThreshold = maxBytes;
                form.BufferBodyLengthLimit = maxBytes;
                form.ValueCountLimit = MaxFormFields;
            });

        return services;
    }
}
