// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Api;
using LecturaFacil.Api.Auth;
using LecturaFacil.Api.Configuration;
using LecturaFacil.Api.Documents;
using LecturaFacil.Api.Health;
using LecturaFacil.Api.Llm;
using LecturaFacil.Api.Observability;
using LecturaFacil.Api.Security;
using LecturaFacil.Application;
using LecturaFacil.Infrastructure;
using LecturaFacil.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
builder.AddObservability();

builder.Services
    .AddValidatedOptions()
    .AddApplication()
    .AddInfrastructure()
    .AddApi();

var app = builder.Build();

app.ValidateOptionsOrStop();
app.UseApi();
app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapDocumentEndpoints();
app.MapLlmDiagnosticsEndpoints(app.Environment);
app.MapLlmStatusEndpoints();
app.MapMonitoringEndpoints();
app.MapMetricsEndpoints();

if (app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value.ApplyMigrationsOnStartup)
{
    await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);
}

await app.SeedTestUserAsync(app.Lifetime.ApplicationStopping);

await app.RunAsync();
