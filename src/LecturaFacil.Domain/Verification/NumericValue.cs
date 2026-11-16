// SPDX-License-Identifier: Apache-2.0
using System.Globalization;

namespace LecturaFacil.Domain.Verification;

/// <summary>
/// Valor de una cifra ya normalizada: un número ("quince", "15" y "15,0" valen lo mismo) o una fecha completa
/// ("15 de diciembre de 2026" y "15/12/2026" valen lo mismo). La unidad (€, %, días) no forma parte del valor.
/// </summary>
public readonly record struct NumericValue
{
    private NumericValue(decimal? number, DateOnly? date)
    {
        Number = number;
        Date = date;
    }

    public decimal? Number { get; }

    public DateOnly? Date { get; }

    /// <summary>Un número. Se quitan los ceros decimales sobrantes para que 2,50 y 2,5 sean el mismo valor.</summary>
    public static NumericValue Of(decimal number) => new(number / 1.000000000000000000000000000000000m, null);

    public static NumericValue Of(DateOnly date) => new(null, date);

    public override string ToString() =>
        Date is { } date
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : Number!.Value.ToString(CultureInfo.InvariantCulture);
}
