// SPDX-License-Identifier: Apache-2.0
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Documents;

/// <summary>
/// Una página de "Mis documentos": filtro por título y por estado, y el punto desde el que seguir.
/// El orden es siempre del más nuevo al más antiguo (fecha de creación y, si empatan, identificador).
/// </summary>
/// <param name="TitleContains">Texto que debe aparecer en el título, sin distinguir mayúsculas ni tildes. <c>null</c> = cualquiera.</param>
/// <param name="Statuses">Estados admitidos. Vacío = todos.</param>
/// <param name="After">Último documento de la página anterior; <c>null</c> para la primera página.</param>
/// <param name="PageSize">Documentos por página.</param>
public sealed record DocumentListQuery(
    string? TitleContains,
    IReadOnlySet<DocumentStatus> Statuses,
    DocumentListCursor? After,
    int PageSize)
{
    private const CompareOptions TitleComparison = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>
    /// Misma regla que la consulta a la base de datos, para los repositorios en memoria (pruebas y evaluaciones).
    /// No comprueba el propietario: eso lo hace cada repositorio.
    /// </summary>
    public bool Matches(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (TitleContains is { Length: > 0 } title
            && CultureInfo.InvariantCulture.CompareInfo.IndexOf(document.Title.Value, title, TitleComparison) < 0)
        {
            return false;
        }

        if (Statuses.Count > 0 && !Statuses.Contains(document.Status))
        {
            return false;
        }

        return After is not { } after || after.IsBefore(document.CreatedAt, document.Id.Value);
    }
}

/// <summary>
/// Posición en la lista: fecha de creación e identificador del último documento ya mostrado. Viaja como texto
/// opaco (<see cref="Encode"/>); el cliente no debe interpretarlo.
/// </summary>
public readonly record struct DocumentListCursor(DateTimeOffset CreatedAt, Guid Id)
{
    private const char Separator = '_';

    // Un cursor válido ocupa 68 caracteres: lo que pase de aquí no se intenta leer.
    private const int MaxEncodedLength = 96;

    public static DocumentListCursor From(DocumentSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new DocumentListCursor(summary.CreatedAt, summary.Id.Value);
    }

    /// <summary><c>true</c> si un documento con esa fecha e identificador va después del cursor en la lista.</summary>
    public bool IsBefore(DateTimeOffset createdAt, Guid id) =>
        createdAt < CreatedAt || (createdAt == CreatedAt && id.CompareTo(Id) < 0);

    public string Encode()
    {
        var raw = string.Create(CultureInfo.InvariantCulture, $"{CreatedAt.UtcTicks}{Separator}{Id:N}");
        return Base64Url.EncodeToString(Encoding.ASCII.GetBytes(raw));
    }

    /// <summary>Lee un cursor de <see cref="Encode"/>. Devuelve <c>false</c> si el texto no es un cursor válido.</summary>
    public static bool TryDecode(string? value, out DocumentListCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrEmpty(value) || value.Length > MaxEncodedLength)
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Base64Url.DecodeFromChars(value);
        }
        catch (FormatException)
        {
            return false;
        }

        var parts = Encoding.ASCII.GetString(bytes).Split(Separator);
        if (parts.Length != 2
            || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks < DateTimeOffset.MinValue.UtcTicks
            || ticks > DateTimeOffset.MaxValue.UtcTicks
            || !Guid.TryParseExact(parts[1], "N", out var id)
            || id == Guid.Empty)
        {
            return false;
        }

        cursor = new DocumentListCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        return true;
    }
}

/// <summary>Una página de la lista y, si hay más documentos, el cursor para pedir la siguiente.</summary>
public sealed record DocumentPage(IReadOnlyList<DocumentSummary> Items, DocumentListCursor? Next)
{
    /// <summary>
    /// Construye la página a partir de hasta <paramref name="pageSize"/> + 1 documentos ya ordenados: el de más solo
    /// indica que hay otra página y no se devuelve.
    /// </summary>
    public static DocumentPage FromFetched(IReadOnlyList<DocumentSummary> fetched, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(fetched);
        if (fetched.Count <= pageSize)
        {
            return new DocumentPage(fetched, null);
        }

        var items = fetched.Take(pageSize).ToList();
        return new DocumentPage(items, DocumentListCursor.From(items[^1]));
    }
}
