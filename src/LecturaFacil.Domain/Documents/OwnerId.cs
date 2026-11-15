// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>Identificador del usuario propietario de un documento. Nunca es <see cref="Guid.Empty"/>.</summary>
public readonly record struct OwnerId
{
    private OwnerId(Guid value) => Value = value;

    public Guid Value { get; }

    public static OwnerId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An owner id cannot be empty.", nameof(value));
        }

        return new OwnerId(value);
    }

    public override string ToString() => Value.ToString();
}
