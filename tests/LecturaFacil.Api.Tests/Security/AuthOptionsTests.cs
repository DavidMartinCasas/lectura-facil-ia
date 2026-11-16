// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;
using LecturaFacil.Api.Security;

namespace LecturaFacil.Api.Tests.Security;

public sealed class AuthOptionsTests
{
    [Fact]
    public void Defaults_AreThoseOfTheStory()
    {
        var options = new AuthOptions();

        options.MaxFailedAccessAttempts.ShouldBe(5);
        options.LockoutMinutes.ShouldBe(15);
        options.SessionHours.ShouldBe(8);
        AuthOptions.MinimumPasswordLength.ShouldBe(12);
        Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true).ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenTheLockoutIsDisabledByConfiguration_IsNotValid()
    {
        var options = new AuthOptions { MaxFailedAccessAttempts = 0 };

        Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true).ShouldBeFalse();
    }
}
