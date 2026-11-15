// SPDX-License-Identifier: Apache-2.0
using System.Reflection;

namespace LecturaFacil.Application.Common;

/// <summary>
/// Marca una opción de configuración que contiene un secreto (claves, contraseñas, cadenas de conexión).
/// Su valor nunca se registra ni se muestra en mensajes de error o endpoints de diagnóstico.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class SecretAttribute : Attribute
{
    public static bool IsSecret(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.IsDefined(typeof(SecretAttribute), inherit: true);
    }
}
