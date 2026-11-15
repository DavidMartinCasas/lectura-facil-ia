// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Persistence;
using Npgsql;

namespace LecturaFacil.Infrastructure.Tests.Persistence;

public sealed class DatabaseConnectionStringTests
{
    private const string BaseConnectionString = "Host=postgres;Port=5432";

    [Theory]
    [InlineData("abc;def")]
    [InlineData("a=b;Host=evil")]
    [InlineData("con 'comillas' y \"dobles\"")]
    public void Build_WhenCredentialsContainSeparators_KeepsThemIntact(string password)
    {
        var connectionString = DatabaseConnectionString.Build(BaseConnectionString, "lectura;facil", "usuario;raro", password);

        var parsed = new NpgsqlConnectionStringBuilder(connectionString);
        parsed.Host.ShouldBe("postgres");
        parsed.Port.ShouldBe(5432);
        parsed.Database.ShouldBe("lectura;facil");
        parsed.Username.ShouldBe("usuario;raro");
        parsed.Password.ShouldBe(password);
    }

    [Fact]
    public void Build_WhenNoSeparateCredentials_KeepsBaseConnectionString()
    {
        var connectionString = DatabaseConnectionString.Build(
            "Host=localhost;Database=db;Username=u;Password=p", null, null, null);

        var parsed = new NpgsqlConnectionStringBuilder(connectionString);
        parsed.Database.ShouldBe("db");
        parsed.Username.ShouldBe("u");
        parsed.Password.ShouldBe("p");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Database=db")]
    public void Build_WhenHostIsMissing_Throws(string? baseConnectionString)
    {
        Should.Throw<InvalidOperationException>(() => DatabaseConnectionString.Build(baseConnectionString, null, null, null));
    }
}
