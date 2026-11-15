// SPDX-License-Identifier: Apache-2.0
using Npgsql;

namespace LecturaFacil.Infrastructure.Persistence;

/// <summary>
/// Construye la cadena de conexión a partir de una base sin credenciales y de las credenciales por separado.
/// <see cref="NpgsqlConnectionStringBuilder"/> escapa cada valor, así que una contraseña con <c>;</c> o <c>=</c>
/// no rompe la cadena ni puede inyectar otras claves.
/// </summary>
public static class DatabaseConnectionString
{
    public static string Build(string? baseConnectionString, string? database, string? username, string? password)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString ?? string.Empty);

        if (string.IsNullOrWhiteSpace(builder.Host))
        {
            throw new InvalidOperationException("The database connection string must define a host.");
        }

        if (!string.IsNullOrEmpty(database))
        {
            builder.Database = database;
        }

        if (!string.IsNullOrEmpty(username))
        {
            builder.Username = username;
        }

        if (!string.IsNullOrEmpty(password))
        {
            builder.Password = password;
        }

        return builder.ConnectionString;
    }
}
