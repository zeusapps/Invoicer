using System.Xml;
using System.Xml.Schema;

namespace Invoicer.Tests;

/// <summary>
/// Serves the FA(3) schema closure from the vendored copies in TestData/Schema.
/// The copies keep their original absolute crd.gov.pl schemaLocation attributes, so
/// resolution is remapped here rather than by editing the published files. Any URI
/// outside the closure throws, which turns an accidental network dependency into a
/// test failure instead of a silent download.
/// </summary>
internal sealed class LocalSchemaResolver : XmlResolver
{
    private static readonly string[] KnownSchemas =
    [
        "schemat.xsd",
        "StrukturyDanych_v10-0E.xsd",
        "ElementarneTypyDanych_v10-0E.xsd",
        "KodyKrajow_v10-0E.xsd",
    ];

    public static string SchemaDirectory { get; } =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Schema");

    public static string RootSchemaPath { get; } = Path.Combine(SchemaDirectory, "schemat.xsd");

    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        var requested = Path.GetFileName(absoluteUri.AbsolutePath);
        var known = Array.Find(KnownSchemas, s => string.Equals(s, requested, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Refused to resolve '{absoluteUri}'. Only the vendored FA(3) schema closure may be loaded, " +
                "and schema resolution must never reach the network.");

        var path = Path.Combine(SchemaDirectory, known);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Vendored schema '{known}' is missing from '{SchemaDirectory}'.", path);
        }

        return File.OpenRead(path);
    }
}

/// <summary>
/// Validates generated invoices against the official FA(3) schema.
/// </summary>
internal static class KsefSchemaValidator
{
    /// <summary>
    /// Returns every validation error and warning for the document at <paramref name="xmlPath"/>,
    /// empty when the document is valid. Warnings count: a document whose namespace matches no
    /// schema in the set produces only a warning, and treating that as a pass would mean the
    /// validation silently stopped validating anything.
    /// </summary>
    public static IReadOnlyList<string> Validate(string xmlPath)
    {
        var messages = new List<string>();
        var resolver = new LocalSchemaResolver();

        var schemas = new XmlSchemaSet { XmlResolver = resolver };
        schemas.ValidationEventHandler += (_, e) => messages.Add(Describe("schema", e));

        var schemaReaderSettings = new XmlReaderSettings
        {
            XmlResolver = resolver,
            DtdProcessing = DtdProcessing.Prohibit,
        };

        using (var schemaReader = XmlReader.Create(LocalSchemaResolver.RootSchemaPath, schemaReaderSettings))
        {
            schemas.Add(targetNamespace: null, schemaReader);
        }

        schemas.Compile();

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemas,
            XmlResolver = resolver,
            DtdProcessing = DtdProcessing.Prohibit,
            ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
                | XmlSchemaValidationFlags.ProcessIdentityConstraints,
        };
        settings.ValidationEventHandler += (_, e) => messages.Add(Describe("document", e));

        // Opened as a stream so the document itself does not go through the resolver,
        // which only allows the four schema files.
        using var stream = File.OpenRead(xmlPath);
        using var reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
        }

        return messages;
    }

    /// <summary>
    /// Formats the accumulated messages for an assertion failure, so a broken document
    /// reports every violation with its position rather than only the first.
    /// </summary>
    public static string Format(IReadOnlyList<string> messages) =>
        string.Join(Environment.NewLine, messages.Select(m => "  " + m));

    private static string Describe(string stage, ValidationEventArgs e)
    {
        var position = e.Exception is null
            ? string.Empty
            : $" at line {e.Exception.LineNumber}, position {e.Exception.LinePosition}";

        return $"[{stage} {e.Severity.ToString().ToLowerInvariant()}]{position}: {e.Message}";
    }
}
