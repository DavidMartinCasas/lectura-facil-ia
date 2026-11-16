// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Users;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Tests.Users;

public sealed class SeedTestUserTests
{
    private const string Email = "persona.prueba@example.org";
    private const string Password = "contraseña-de-prueba-larga";

    private readonly InMemoryUserAccounts _accounts = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Execute_WhenTheUserDoesNotExist_CreatesIt()
    {
        var result = await CreateUseCase(Email, Password).ExecuteAsync(Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(SeedOutcome.Created);
        _accounts.Created.ShouldBe([Email]);
    }

    [Fact]
    public async Task Execute_WhenRunTwice_DoesNotDuplicateTheUser()
    {
        var useCase = CreateUseCase(Email, Password);

        await useCase.ExecuteAsync(Token);
        var second = await useCase.ExecuteAsync(Token);

        second.Value.ShouldBe(SeedOutcome.AlreadyExists);
        _accounts.Created.ShouldBe([Email]);
    }

    [Fact]
    public async Task Execute_WhenTheUserAlreadyExists_DoesNotChangeItsPassword()
    {
        _accounts.Existing.Add(Email);

        var result = await CreateUseCase(Email, "otra-contraseña-distinta").ExecuteAsync(Token);

        result.Value.ShouldBe(SeedOutcome.AlreadyExists);
        _accounts.Created.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", " ")]
    public async Task Execute_WhenNotConfigured_DoesNothing(string? email, string? password)
    {
        var result = await CreateUseCase(email, password).ExecuteAsync(Token);

        result.Value.ShouldBe(SeedOutcome.NotConfigured);
        _accounts.Created.ShouldBeEmpty();
        _accounts.Lookups.ShouldBe(0);
    }

    [Fact]
    public async Task Execute_WhenTheAccountCannotBeCreated_ReturnsTheError()
    {
        _accounts.CreateError = UserErrors.SeedFailed(["PasswordTooShort"]);

        var result = await CreateUseCase(Email, Password).ExecuteAsync(Token);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe(UserErrors.SeedFailedCode);
        result.Error.Message.ShouldContain("PasswordTooShort");
        result.Error.Message.ShouldNotContain(Password);
    }

    [Fact]
    public void Options_WithNothingConfigured_AreValid()
    {
        Validate(new TestUserOptions()).ShouldBeEmpty();
    }

    [Fact]
    public void Options_WithEmailAndPassword_AreValid()
    {
        Validate(new TestUserOptions { Email = Email, Password = Password }).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(Email, null)]
    [InlineData(null, Password)]
    [InlineData("no-es-un-correo", Password)]
    public void Options_WhenIncompleteOrInvalid_AreNotValid(string? email, string? password)
    {
        var errors = Validate(new TestUserOptions { Email = email, Password = password });

        errors.ShouldNotBeEmpty();
        errors.ShouldAllBe(error => !error.Contains(Password, StringComparison.Ordinal));
    }

    [Fact]
    public void Options_Password_IsMarkedAsSecret()
    {
        SecretAttribute.IsSecret(typeof(TestUserOptions).GetProperty(nameof(TestUserOptions.Password))!).ShouldBeTrue();
    }

    private SeedTestUser CreateUseCase(string? email, string? password) =>
        new(_accounts, Options.Create(new TestUserOptions { Email = email, Password = password }));

    private static List<string> Validate(TestUserOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return [.. results.Select(result => result.ErrorMessage ?? string.Empty)];
    }

    private sealed class InMemoryUserAccounts : IUserAccounts
    {
        public HashSet<string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Created { get; } = [];

        public Error? CreateError { get; set; }

        public int Lookups { get; private set; }

        public Task<bool> ExistsAsync(string email, CancellationToken cancellationToken)
        {
            Lookups++;
            return Task.FromResult(Existing.Contains(email));
        }

        public Task<Result> CreateAsync(string email, string password, CancellationToken cancellationToken)
        {
            if (CreateError is not null)
            {
                return Task.FromResult(Result.Failure(CreateError));
            }

            Existing.Add(email);
            Created.Add(email);
            return Task.FromResult(Result.Success());
        }
    }
}
