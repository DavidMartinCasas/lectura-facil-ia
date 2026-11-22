// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using LecturaFacil.Api.Auth;
using LecturaFacil.Api.Security;
using LecturaFacil.Application.Users;
using LecturaFacil.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Api.Tests.Security;

/// <summary>Inicio de sesión real con Identity y PostgreSQL (US-15): cookie, mensajes genéricos, bloqueo, logout y BOLA.</summary>
public sealed class LoginFlowTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private static readonly Uri LoginUri = new("/api/auth/login", UriKind.Relative);
    private static readonly Uri LogoutUri = new("/api/auth/logout", UriKind.Relative);
    private static readonly Uri MeUri = new("/api/auth/me", UriKind.Relative);
    private static readonly Uri DocumentsUri = new("/api/documents", UriKind.Relative);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_WithTheSeededUser_OpensTheSessionWithAnHttpOnlySecureStrictCookie()
    {
        using var client = await factory.CreateCookieClientAsync();

        using var response = await client.PostAsJsonAsync(LoginUri, new { email = factory.SeededEmail, password = factory.SeededPassword }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<CurrentUserResponse>(Token)).ShouldNotBeNull().Email.ShouldBe(factory.SeededEmail);
        var cookie = SessionCookie(response).ShouldNotBeNull();
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("secure", Case.Insensitive);
        cookie.ShouldContain("samesite=strict", Case.Insensitive);
        cookie.ShouldContain("path=/", Case.Insensitive);
        client.UseAntiforgeryTokenFrom(response);
        using var documents = await client.GetAsync(DocumentsUri, Token);
        documents.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithAWrongPasswordOrAnUnknownEmail_Returns401WithTheSameGenericMessage()
    {
        using var client = await factory.CreateCookieClientAsync();

        using var wrongPassword = await client.PostAsJsonAsync(LoginUri, new { email = factory.SeededEmail, password = IdentityApiFactory.NewPassword() }, Token);
        using var unknownEmail = await client.PostAsJsonAsync(LoginUri, new { email = "nadie@example.org", password = IdentityApiFactory.NewPassword() }, Token);

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var wrongPasswordBody = await wrongPassword.Content.ReadAsStringAsync(Token);
        wrongPasswordBody.ShouldContain(AuthEndpoints.InvalidCredentialsMessage);
        wrongPasswordBody.ShouldContain(AuthEndpoints.InvalidCredentialsCode);
        Documents.GetDocumentEndpointTests.WithoutTraceId(wrongPasswordBody)
            .ShouldBe(Documents.GetDocumentEndpointTests.WithoutTraceId(await unknownEmail.Content.ReadAsStringAsync(Token)));
        SessionCookie(wrongPassword).ShouldBeNull();
    }

    [Fact]
    public async Task Login_AfterFiveConsecutiveFailures_LocksTheAccountFor15Minutes()
    {
        var (email, password) = await CreateUserAsync();
        using var client = await factory.CreateCookieClientAsync();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failed = await client.PostAsJsonAsync(LoginUri, new { email, password = IdentityApiFactory.NewPassword() }, Token);
            failed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var withTheRightPassword = await client.PostAsJsonAsync(LoginUri, new { email, password }, Token);

        withTheRightPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await withTheRightPassword.Content.ReadAsStringAsync(Token)).ShouldContain(AuthEndpoints.InvalidCredentialsMessage);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email)).ShouldNotBeNull();
        var lockedFor = user.LockoutEnd.ShouldNotBeNull() - DateTimeOffset.UtcNow;
        lockedFor.ShouldBeInRange(TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task Logout_InvalidatesTheSessionCookieOnTheServer()
    {
        var (email, password) = await CreateUserAsync();
        using var client = await factory.CreateCookieClientAsync();
        using var login = await client.PostAsJsonAsync(LoginUri, new { email, password }, Token);
        client.UseAntiforgeryTokenFrom(login);
        var stolenCookie = SessionCookie(login).ShouldNotBeNull().Split(';')[0];

        using var logout = await client.PostAsync(LogoutUri, content: null, Token);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var afterLogout = await client.GetAsync(MeUri, Token);
        afterLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var replay = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = ApiClients.BaseAddress, HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, MeUri);
        request.Headers.Add("Cookie", stolenCookie);
        using var replayed = await replay.SendAsync(request, Token);
        replayed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Document_OfAnotherUser_Returns404WithRealSessions()
    {
        var userA = await CreateUserAsync();
        var userB = await CreateUserAsync();
        using var clientOfB = await LoginAsync(userB.Email, userB.Password);
        using var submitted = await clientOfB.PostAsJsonAsync(DocumentsUri, new { text = DocumentText, title = "De B" }, Token);
        submitted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var documentOfB = (await submitted.Content.ReadFromJsonAsync<SubmittedDocument>(Token)).ShouldNotBeNull().Id;
        using var clientOfA = await LoginAsync(userA.Email, userA.Password);

        using var byA = await clientOfA.GetAsync(new Uri($"/api/documents/{documentOfB}", UriKind.Relative), Token);
        using var byB = await clientOfB.GetAsync(new Uri($"/api/documents/{documentOfB}", UriKind.Relative), Token);
        var listOfA = await clientOfA.GetFromJsonAsync<ListedPage>(DocumentsUri, Token);

        byA.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        byB.StatusCode.ShouldBe(HttpStatusCode.OK);
        listOfA.ShouldNotBeNull().Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Seed_WhenRunAgain_DoesNotDuplicateTheTestUser()
    {
        using var scope = factory.Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<SeedTestUser>().ExecuteAsync(Token);

        result.Value.ShouldBe(SeedOutcome.AlreadyExists);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var normalized = users.NormalizeEmail(factory.SeededEmail);
        users.Users.Count(user => user.NormalizedEmail == normalized).ShouldBe(1);
    }

    private const string DocumentText =
        "La solicitud de la ayuda se presenta en el registro del ayuntamiento antes del 30 de junio de 2026.";

    private async Task<(string Email, string Password)> CreateUserAsync()
    {
        var email = $"persona-{Guid.NewGuid():N}@example.org";
        var password = IdentityApiFactory.NewPassword();
        using var scope = factory.Services.CreateScope();
        var created = await scope.ServiceProvider.GetRequiredService<IUserAccounts>().CreateAsync(email, password, Token);
        created.IsSuccess.ShouldBeTrue();
        return (email, password);
    }

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = await factory.CreateCookieClientAsync();
        using var response = await client.PostAsJsonAsync(LoginUri, new { email, password }, Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        client.UseAntiforgeryTokenFrom(response);
        return client;
    }

    private static string? SessionCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.LastOrDefault(cookie => cookie.StartsWith(AuthenticationConfiguration.SessionCookieName + "=", StringComparison.Ordinal))
            : null;

    private sealed record SubmittedDocument(Guid Id);

    private sealed record ListedPage(List<SubmittedDocument> Items, string? NextCursor);
}
