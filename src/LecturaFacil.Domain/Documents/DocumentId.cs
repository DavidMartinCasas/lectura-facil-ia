// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>Identificador de un documento. Nunca es <see cref="Guid.Empty"/>.</summary>
public readonly record struct DocumentId
{
    private DocumentId(Guid value) => Value = value;

    public Guid Value { get; }

    public static DocumentId New() => new(Guid.CreateVersion7());

    public static DocumentId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A document id cannot be empty.", nameof(value));
        }

        return new DocumentId(value);
    }

    public override string ToString() => Value.ToString();
}
