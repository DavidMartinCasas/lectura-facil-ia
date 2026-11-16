// SPDX-License-Identifier: Apache-2.0
using System.Globalization;

namespace LecturaFacil.Domain.Verification;

/// <summary>
/// Valor de una cifra ya normalizada: un número ("quince", "15" y "15,0" valen lo mismo), una fecha completa
/// ("15 de diciembre de 2026" y "15/12/2026" valen lo mismo) o un día de un mes sin año ("15 de diciembre"). La unidad
/// (€, %, días) no forma parte del valor: la comprueba <see cref="QualifierCheck"/>.
/// </summary>
public readonly record struct NumericValue
{
    private const int MonthFactor = 100;

    private NumericValue(decimal? number, DateOnly? date, int? monthDay)
    {
        Number = number;
        Date = date;
        MonthDay = monthDay;
    }

    public decimal? Number { get; }

    public DateOnly? Date { get; }

    /// <summary>Mes × 100 + día, para las fechas sin año: "15 de diciembre" → 1215.</summary>
    public int? MonthDay { get; }

    /// <summary>Un número. Se quitan los ceros decimales sobrantes para que 2,50 y 2,5 sean el mismo valor.</summary>
    public static NumericValue Of(decimal number) => new(number / 1.000000000000000000000000000000000m, null, null);

    public static NumericValue Of(DateOnly date) => new(null, date, null);

    /// <summary>Un día de un mes, sin año. No comprueba si el día existe: eso lo hace quien lo lee del texto.</summary>
    public static NumericValue OfMonthDay(int month, int day) => new(null, null, (month * MonthFactor) + day);

    public override string ToString() =>
        Date is { } date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : MonthDay is { } monthDay ? string.Create(CultureInfo.InvariantCulture, $"--{monthDay / MonthFactor:00}-{monthDay % MonthFactor:00}")
        : Number!.Value.ToString(CultureInfo.InvariantCulture);
}
