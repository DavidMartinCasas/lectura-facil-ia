// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Documents;

public interface IDocumentRepository
{
    /// <summary>Guarda un documento nuevo.</summary>
    Task AddAsync(Document document, CancellationToken cancellationToken);
}
