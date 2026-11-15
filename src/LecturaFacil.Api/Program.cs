// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Api;
using LecturaFacil.Api.Health;
using LecturaFacil.Application;
using LecturaFacil.Infrastructure;
using LecturaFacil.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi();

var app = builder.Build();

app.UseApi();
app.MapHealthEndpoints();

if (app.Configuration.GetValue<bool>(ApiConfiguration.ApplyMigrationsOnStartupKey))
{
    await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);
}

await app.RunAsync();
