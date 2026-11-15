// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LecturaFacil.Infrastructure.Tests.Persistence;

public sealed class AppDbContextTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Migrate_WhenDatabaseIsEmpty_AppliesAllMigrations()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = CreateContext();

        await context.Database.MigrateAsync(cancellationToken);

        var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
        pending.ShouldBeEmpty();
        (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ShouldNotBeEmpty();
    }

    [Fact]
    public void Model_WhenComparedWithMigrations_HasNoPendingChanges()
    {
        using var context = CreateContext();

        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database.ConnectionString).Options);
}
