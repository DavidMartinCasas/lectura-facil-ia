// SPDX-License-Identifier: Apache-2.0
using FluentValidation;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Documents;

internal sealed class RecordValidationRequestValidator : AbstractValidator<RecordValidationRequest>
{
    public RecordValidationRequestValidator()
    {
        RuleFor(request => request.Version)
            .NotNull().WithMessage("Indica la versión que ha leído el grupo.")
            .GreaterThan(0).WithMessage("La versión no es válida.");
        RuleFor(request => request.Outcome)
            .Must(outcome => Enum.TryParse<ValidationOutcome>(outcome, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
                && !int.TryParse(outcome, out _))
            .WithMessage("Indica si el grupo lo entiende o pide cambios.");
        RuleFor(request => request.ValidatedBy)
            .Must(validatedBy => !string.IsNullOrWhiteSpace(validatedBy))
            .WithMessage("Indica quién ha validado el documento.")
            .MaximumLength(ValidationRecord.ValidatedByMaxLength)
            .WithMessage($"Quién lo ha validado no puede superar {ValidationRecord.ValidatedByMaxLength} caracteres.");
        RuleFor(request => request.Participants)
            .NotNull().WithMessage("Indica cuántas personas lo han leído.")
            .InclusiveBetween(1, ValidationRecord.MaxParticipants)
            .WithMessage($"El número de personas tiene que estar entre 1 y {ValidationRecord.MaxParticipants}.");
        RuleFor(request => request.HardSections)
            .Must(sections => sections is null || sections.Count == 0)
            .When(request => request.Outcome == nameof(ValidationOutcome.Approved))
            .WithMessage("Solo se marcan las partes difíciles cuando el grupo pide cambios.");
        RuleFor(request => request.HardGlossary)
            .Must(glossary => glossary != true)
            .When(request => request.Outcome == nameof(ValidationOutcome.Approved))
            .WithMessage("Solo se marcan las partes difíciles cuando el grupo pide cambios.");
        RuleFor(request => request.HardSections)
            .Must(sections => sections!.All(index => index >= 0) && sections!.Distinct().Count() == sections!.Count)
            .When(request => request.HardSections is not null)
            .WithMessage("Las secciones marcadas no son válidas.");
        RuleFor(request => request.Notes)
            .MaximumLength(ValidationRecord.NotesMaxLength)
            .WithMessage($"Las observaciones no pueden superar {ValidationRecord.NotesMaxLength} caracteres.");
    }
}
