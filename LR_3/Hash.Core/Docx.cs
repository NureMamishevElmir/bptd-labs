using System.IO.Compression;
using System.Security;
using System.Text;

namespace Hash.Core;

/// <summary>Мінімальний .docx: збирання zip-пакета вручну та вставка XML-коментаря.</summary>
public static class Docx
{
    public const string DocumentPart = "word/document.xml";
    static readonly DateTimeOffset Stamp = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero); // фіксована дата → детермінований zip

    /// <summary>Створює документ з абзаців (перший — жирний заголовок).</summary>
    public static byte[] Create(string title, IEnumerable<string> paragraphs)
    {
        var body = new StringBuilder();
        body.Append(Paragraph(title, bold: true, center: true));
        foreach (var p in paragraphs) body.Append(Paragraph(p, false, false));

        string doc = $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{body}<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="850" w:bottom="1134" w:left="1701" w:header="708" w:footer="708" w:gutter="0"/></w:sectPr></w:body></w:document>
            """;

        return Pack(new List<KeyValuePair<string, byte[]>>
        {
            new("[Content_Types].xml", Utf8("""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/></Types>
                """)),
            new("_rels/.rels", Utf8("""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/></Relationships>
                """)),
            new("docProps/core.xml", Utf8($"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>{SecurityElement.Escape(title)}</dc:title><dc:creator>Мамішев Е. І.</dc:creator></cp:coreProperties>
                """)),
            new(DocumentPart, Utf8(doc)),
        });
    }

    static string Paragraph(string text, bool bold, bool center) =>
        "<w:p><w:pPr>" + (center ? "<w:jc w:val=\"center\"/>" : "<w:jc w:val=\"both\"/>") + "</w:pPr>" +
        "<w:r><w:rPr><w:rFonts w:ascii=\"Times New Roman\" w:hAnsi=\"Times New Roman\" w:cs=\"Times New Roman\"/>" +
        (bold ? "<w:b/>" : "") + "<w:sz w:val=\"28\"/></w:rPr>" +
        $"<w:t xml:space=\"preserve\">{SecurityElement.Escape(text)}</w:t></w:r></w:p>";

    static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    /// <summary>Пакує частини у zip (порядок зберігається, дата фіксована).</summary>
    public static byte[] Pack(IEnumerable<KeyValuePair<string, byte[]>> parts)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in parts)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                entry.LastWriteTime = Stamp;
                using var s = entry.Open();
                s.Write(data);
            }
        }
        return ms.ToArray();
    }

    /// <summary>Розпаковує всі частини пакета.</summary>
    public static List<KeyValuePair<string, byte[]>> Unpack(byte[] docx)
    {
        using var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        return zip.Entries.Select(e =>
        {
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return new KeyValuePair<string, byte[]>(e.FullName, ms.ToArray());
        }).ToList();
    }

    public static bool IsDocx(byte[] data)
    {
        if (data.Length < 4 || data[0] != 'P' || data[1] != 'K') return false;
        try { return Unpack(data).Any(p => p.Key == DocumentPart); }
        catch (InvalidDataException) { return false; }
    }

    public static string ReadDocumentXml(byte[] docx) =>
        Encoding.UTF8.GetString(Unpack(docx).First(p => p.Key == DocumentPart).Value);

    /// <summary>Вставляє прихований коментар &lt;!-- ... --&gt; перед &lt;/w:document&gt;.</summary>
    public static string InsertComment(string documentXml, string comment)
    {
        int end = documentXml.LastIndexOf("</w:document>", StringComparison.Ordinal);
        if (end < 0) throw new InvalidDataException("Не знайдено </w:document>");
        return documentXml.Insert(end, $"<!-- {comment} -->");
    }
}
