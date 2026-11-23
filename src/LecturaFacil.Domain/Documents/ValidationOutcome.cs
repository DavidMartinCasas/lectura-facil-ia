// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>Lo que decidió el grupo de validación al leer el borrador.</summary>
public enum ValidationOutcome
{
    /// <summary>El grupo lo entiende: el documento queda <see cref="DocumentStatus.Validated"/>.</summary>
    Approved,

    /// <summary>El grupo pide cambios: el documento vuelve a <see cref="DocumentStatus.Draft"/> para corregirlo.</summary>
    ChangesRequested,
}
