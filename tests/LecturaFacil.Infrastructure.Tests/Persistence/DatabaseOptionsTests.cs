// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Persistence;
using Npgsql;

namespace LecturaFacil.Infrastructure.Tests.Persistence;

public sealed class DatabaseOptionsTests
{
    private const string SecretValue = "secreto-secreto-secreto";

    [Fact]
    public void Validate_WhenConnectionStringHasHost_IsValid()
    {
        OptionsValidation.Validate(new DatabaseOptions { ConnectionString = "Host=postgres;Port=5432" }).ShouldBeEmpty();
    }

    [Fact]
    public void ApplyMigrationsOnStartup_ByDefault_IsFalse()
    {
        new DatabaseOptions().ApplyMigrationsOnStartup.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Database=db")]
    [InlineData("esto no es una cadena")]
    public void Validate_WhenConnectionStringIsMissingOrInvalid_ReportsWhichOption(string connectionString)
    {
        var errors = OptionsValidation.Validate(new DatabaseOptions { ConnectionString = connectionString });

        errors.ShouldHaveSingleItem().ShouldContain("Database:ConnectionString");
    }

    [Fact]
    public void Validate_WhenConnectionStringIsInvalid_DoesNotIncludeItsValue()
    {
        var connectionString = $"Database=db;Password={SecretValue}";

        var errors = OptionsValidation.Validate(new DatabaseOptions { ConnectionString = connectionString, Password = SecretValue });

        errors.ShouldAllBe(error => !error.Contains(SecretValue, StringComparison.Ordinal));
    }

    [Fact]
    public void BuildConnectionString_WhenCredentialsAreSeparate_CombinesThem()
    {
        var options = new DatabaseOptions
        {
            ConnectionString = "Host=postgres;Port=5432",
            Name = "lecturafacil",
            Username = "usuario",
            Password = "a;b=c",
        };

        var parsed = new NpgsqlConnectionStringBuilder(options.BuildConnectionString());

        parsed.Host.ShouldBe("postgres");
        parsed.Database.ShouldBe("lecturafacil");
        parsed.Username.ShouldBe("usuario");
        parsed.Password.ShouldBe("a;b=c");
    }
}
