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
    /// <remarks>
    /// <para>
    /// This method requires both <typeparamref name="T"/> and <typeparamref name="TMap"/> to be specified
    /// explicitly at call sites, even though <typeparamref name="T"/> could theoretically be inferred from
    /// the <paramref name="records"/> parameter.
    /// </para>
    /// <para>
    /// C# 14 extension blocks would allow defining this as a method where only <typeparamref name="TMap"/>
    /// needs to be specified. However, extension block methods with non-inferable type parameters (i.e.,
    /// type parameters that only appear in constraints, not in the parameter list) are not supported in
    /// the current preview. Since <typeparamref name="TMap"/> only appears in the constraint
    /// <c>where TMap : ClassMap&lt;T&gt;, new()</c> and not as a method parameter, this pattern cannot
    /// be used.
    /// </para>
    /// </remarks>
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
