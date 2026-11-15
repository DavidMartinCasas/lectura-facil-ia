// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;

namespace LecturaFacil.Api.Configuration;

/// <summary>Textos configurables de la interfaz.</summary>
public sealed class UiOptions
{
    public const string SectionName = "Ui";
    public const int MaxNoticeLength = 500;

    /// <summary>Aviso opcional, por ejemplo "Instancia de demostración: no subas datos personales". Vacío por defecto.</summary>
    [MaxLength(MaxNoticeLength, ErrorMessage = "Ui:Notice no puede superar {1} caracteres.")]
    public string Notice { get; set; } = string.Empty;
}
