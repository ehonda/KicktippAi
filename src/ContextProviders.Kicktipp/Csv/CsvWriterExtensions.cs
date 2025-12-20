using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace ContextProviders.Kicktipp.Csv;

/// <summary>
/// Extension methods for writing objects to CSV using CsvHelper.
/// </summary>
public static class CsvWriterExtensions
{
    /// <summary>
    /// Writes a collection of records to CSV format using the specified ClassMap.
    /// </summary>
    /// <typeparam name="T">The type of records to write.</typeparam>
    /// <typeparam name="TMap">The ClassMap defining the CSV schema.</typeparam>
    /// <param name="records">The collection of records to write.</param>
    /// <returns>The CSV content as a string.</returns>
    public static string WriteToCsv<T, TMap>(this IEnumerable<T> records)
        where T : class
        where TMap : ClassMap<T>, new()
    {
        using var stringWriter = new StringWriter();
        using var csvWriter = new CsvWriter(stringWriter, CultureInfo.InvariantCulture);
        
        csvWriter.Context.RegisterClassMap<TMap>();
        csvWriter.WriteRecords(records);
        
        return stringWriter.ToString();
    }
}
