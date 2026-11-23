// SPDX-License-Identifier: Apache-2.0
using FluentValidation;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Documents;

internal sealed class RecordValidationRequestValidator : AbstractValidator<RecordValidationRequest>
{
    public RecordValidationRequestValidator()
    {
        RuleFor(request => request.Outcome)
            .Must(outcome => Enum.TryParse<ValidationOutcome>(outcome, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
                && !int.TryParse(outcome, out _))
            .WithMessage("Indica si el grupo lo entiende o pide cambios.");
        RuleFor(request => request.ValidatedBy)
            .Must(validatedBy => !string.IsNullOrWhiteSpace(validatedBy))
            .WithMessage("Indica quién ha validado el documento.")
            .MaximumLength(ValidationRecord.ValidatedByMaxLength)
            .WithMessage($"Quién lo ha validado no puede superar {ValidationRecord.ValidatedByMaxLength} caracteres.");
        RuleFor(request => request.Notes)
            .MaximumLength(ValidationRecord.NotesMaxLength)
            .WithMessage($"Las observaciones no pueden superar {ValidationRecord.NotesMaxLength} caracteres.");
    }
}
