// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Verification;

/// <summary>Estado de un dato clave y su motivo, según el código o según el verificador.</summary>
public readonly record struct KeyFactVerdict(KeyFactStatus Status, KeyFactReason Reason)
{
    public static readonly KeyFactVerdict Conserved = new(KeyFactStatus.Conserved, KeyFactReason.None);

    /// <summary>
    /// Un veredicto coherente: un dato conservado no tiene motivo y un aviso siempre lo tiene. Si se dice "conservado"
    /// con un motivo de aviso, ante la duda queda como <see cref="KeyFactStatus.Review"/>.
    /// </summary>
    public static KeyFactVerdict Create(KeyFactStatus status, KeyFactReason reason)
    {
        if (!Enum.IsDefined(status) || !Enum.IsDefined(reason))
        {
            return new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.NeedsReview);
        }

        return (status, reason) switch
        {
            (KeyFactStatus.Conserved, KeyFactReason.None) => Conserved,
            (KeyFactStatus.Conserved, _) => new KeyFactVerdict(KeyFactStatus.Review, reason),
            (_, KeyFactReason.None) => new KeyFactVerdict(status, KeyFactReason.NeedsReview),
            _ => new KeyFactVerdict(status, reason),
        };
    }

    /// <summary>
    /// Regla de combinación: el resultado es el más grave de los dos, así el verificador solo puede añadir avisos y
    /// nunca quitar los del código. Si los dos coinciden en gravedad, se queda el motivo del verificador cuando es más
    /// concreto (por ejemplo, "Conversión calculada") y, si no, el del código.
    /// </summary>
    public static KeyFactVerdict MostSevere(KeyFactVerdict code, KeyFactVerdict verifier)
    {
        if (verifier.Status != code.Status)
        {
            return verifier.Status > code.Status ? verifier : code;
        }

        return verifier.Reason is KeyFactReason.None or KeyFactReason.NeedsReview ? code : verifier;
    }
}
