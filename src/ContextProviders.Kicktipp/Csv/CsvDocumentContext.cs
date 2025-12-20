using CsvHelper.Configuration;
using EHonda.KicktippAi.Core;

namespace ContextProviders.Kicktipp.Csv;

/// <summary>
/// Extension methods for creating <see cref="DocumentContext"/> from CSV data.
/// </summary>
public static class CsvDocumentContextExtensions
{
    /// <summary>
    /// Creates a <see cref="DocumentContext"/> from a collection of records using the specified ClassMap.
    /// </summary>
    /// <typeparam name="T">The type of records to write.</typeparam>
    /// <typeparam name="TMap">The ClassMap defining the CSV schema.</typeparam>
    /// <param name="records">The collection of records to write.</param>
    /// <param name="fileName">The file name for the document (without .csv extension).</param>
    /// <returns>A DocumentContext containing the CSV content.</returns>
    public static DocumentContext ToCsvDocumentContext<T, TMap>(this IEnumerable<T> records, string fileName)
        where T : class
        where TMap : ClassMap<T>, new()
    {
        var csvContent = records.WriteToCsv<T, TMap>();
        return new DocumentContext(Name: $"{fileName}.csv", Content: csvContent);
    }
}
