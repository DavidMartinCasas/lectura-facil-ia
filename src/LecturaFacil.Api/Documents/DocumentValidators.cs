// SPDX-License-Identifier: Apache-2.0
using FluentValidation;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Api.Documents;

/// <summary>Validación en el borde. Domain vuelve a comprobar la longitud una vez normalizado el texto.</summary>
internal sealed class SubmitTextRequestValidator : AbstractValidator<SubmitTextRequest>
{
    public SubmitTextRequestValidator(IOptions<LimitsOptions> limits)
    {
        var options = limits.Value;

        RuleFor(request => request.Text)
            .NotEmpty().WithMessage("Escribe o pega el texto que quieres adaptar.")
            .MaximumLength(options.MaxCharacters).WithMessage(DocumentErrors.TextTooLong(options.MaxCharacters).Message);

        RuleFor(request => request.Title)
            .MaximumLength(DocumentTitle.MaxLength).WithMessage($"El título no puede superar {DocumentTitle.MaxLength} caracteres.");
    }
}

internal sealed class SubmitPdfFormValidator : AbstractValidator<SubmitPdfForm>
{
    public SubmitPdfFormValidator()
    {
        RuleFor(form => form.File)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Adjunta un fichero PDF.")
            .Must(file => file!.Length > 0).WithMessage("El fichero está vacío.")
            .OverridePropertyName(SubmitPdfForm.FieldName);
    }
}
