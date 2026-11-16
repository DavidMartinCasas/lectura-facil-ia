// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Documents;

/// <summary>
/// Otra petición ha cambiado el documento entre que se leyó y se guardó. Los casos de uso que lo modifican a petición
/// del usuario lo traducen a un conflicto (409) para que vuelva a cargarlo; nunca se pisa el cambio del otro.
/// </summary>
public sealed class ConcurrentUpdateException : Exception
{
    public ConcurrentUpdateException()
        : base("The document was changed by another request.")
    {
    }

    public ConcurrentUpdateException(string message)
        : base(message)
    {
    }

    public ConcurrentUpdateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
