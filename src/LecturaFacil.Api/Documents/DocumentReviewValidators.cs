// SPDX-License-Identifier: Apache-2.0
using FluentValidation;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Review;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Api.Documents;

/// <summary>
/// Estructura y tamaño de una edición manual, en el borde. Que nada importante quede vacío tras normalizar lo comprueba
/// Domain (<see cref="LecturaFacil.Domain.Documents.AdaptedContent.TryCreate"/>).
/// </summary>
internal sealed class ManualEditRequestValidator : AbstractValidator<ManualEditRequest>
{
    public ManualEditRequestValidator(IOptions<LimitsOptions> limits)
    {
        var maxCharacters = limits.Value.MaxEditedCharacters;

        RuleFor(request => request.Title).NotNull().WithMessage("Falta el título de la adaptación.");
        RuleFor(request => request.Sections).NotEmpty().WithMessage("La adaptación necesita al menos una sección.");
        RuleForEach(request => request.Sections)
            .Must(IsComplete)
            .WithMessage("Cada sección necesita una pregunta y al menos una frase.");
        RuleForEach(request => request.Glossary)
            .Must(term => term is { Term: not null, Definition: not null })
            .WithMessage("Cada palabra del glosario necesita su explicación.");

        // Cada texto cuenta al menos 1: así también se acota cuántas frases o secciones vacías se pueden enviar.
        RuleFor(request => request)
            .Must(request => Size(request) <= maxCharacters)
            .OverridePropertyName("adaptation")
            .WithMessage(ReviewErrors.EditTooLong(maxCharacters).Message);
    }

    private static bool IsComplete(ManualEditSectionRequest? section) =>
        section?.Question is not null
        && section.Sentences is { Count: > 0 } sentences
        && sentences.All(sentence => sentence is not null);

    private static long Size(ManualEditRequest request)
    {
        var sections = (request.Sections ?? []).OfType<ManualEditSectionRequest>();
        var texts = sections
            .SelectMany(section => (section.Sentences ?? []).Prepend(section.Question))
            .Concat((request.Glossary ?? []).OfType<ManualEditGlossaryTermRequest>().SelectMany(term => new[] { term.Term, term.Definition }))
            .Prepend(request.Title);
        return texts.Sum(text => Math.Max(1L, text?.Length ?? 0));
    }
}

internal sealed class AcknowledgeKeyFactRequestValidator : AbstractValidator<AcknowledgeKeyFactRequest>
{
    public AcknowledgeKeyFactRequestValidator()
    {
        RuleFor(request => request.Version)
            .NotNull().WithMessage("Indica la versión que estás revisando.")
            .GreaterThan(0).WithMessage("La versión no es válida.");
    }
}
