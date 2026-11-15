// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;
using LecturaFacil.Application.Common;
using Npgsql;

namespace LecturaFacil.Infrastructure.Persistence;

/// <summary>
/// Conexión a PostgreSQL. Las credenciales pueden llegar aparte de la cadena (<see cref="Name"/>,
/// <see cref="Username"/>, <see cref="Password"/>) para no tener que escaparlas a mano.
/// </summary>
public sealed class DatabaseOptions : IValidatableObject
{
    public const string SectionName = "Database";

    [Secret]
    public string ConnectionString { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Username { get; set; }

    [Secret]
    public string? Password { get; set; }

    public bool ApplyMigrationsOnStartup { get; set; }

    public string BuildConnectionString() => DatabaseConnectionString.Build(ConnectionString, Name, Username, Password);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!HasHost(ConnectionString))
        {
            yield return new ValidationResult(
                $"{SectionName}:{nameof(ConnectionString)} es obligatoria y debe indicar el servidor (Host).",
                [nameof(ConnectionString)]);
        }
    }

    private static bool HasHost(string connectionString)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(new NpgsqlConnectionStringBuilder(connectionString).Host);
        }
        catch (ArgumentException)
        {
            // El mensaje de Npgsql puede incluir parte de la cadena: no se propaga.
            return false;
        }
    }
}
