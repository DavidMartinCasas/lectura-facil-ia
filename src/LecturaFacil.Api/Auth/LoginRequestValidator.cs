// SPDX-License-Identifier: Apache-2.0
using FluentValidation;
using LecturaFacil.Application.Users;

namespace LecturaFacil.Api.Auth;

/// <summary>Solo longitudes y que no estén vacíos: si las credenciales son correctas lo decide Identity.</summary>
internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Escribe tu correo.")
            .MaximumLength(UserAccountLimits.MaxEmailLength).WithMessage("El correo es demasiado largo.");
        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Escribe tu contraseña.")
            .MaximumLength(UserAccountLimits.MaxPasswordLength).WithMessage("La contraseña es demasiado larga.");
    }
}
