using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Dip.Application.Features.Management;

internal static class ManagementXlsxCodec
{
    private static readonly XNamespace Main="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRel="http://schemas.openxmlformats.org/package/2006/relationships";

    public static byte[] Write(ManagementWorkbookDto workbook)
    {
        using var ms=new MemoryStream();using(var zip=new ZipArchive(ms,ZipArchiveMode.Create,true))
        {
            Add(zip,"[Content_Types].xml",ContentTypes(workbook.Sheets.Count));
            Add(zip,"_rels/.rels",RootRels());
            Add(zip,"xl/workbook.xml",WorkbookXml(workbook.Sheets));
            Add(zip,"xl/_rels/workbook.xml.rels",WorkbookRels(workbook.Sheets.Count));
            Add(zip,"xl/styles.xml",Styles());
            for(var i=0;i<workbook.Sheets.Count;i++)Add(zip,$"xl/worksheets/sheet{i+1}.xml",Worksheet(workbook.Sheets[i]));
            Add(zip,"docProps/core.xml",CoreProps(workbook.GeneratedAtUtc));
            Add(zip,"docProps/app.xml",AppProps(workbook.Sheets));
        }
        return ms.ToArray();
    }

    public static IReadOnlyDictionary<string,IReadOnlyList<IReadOnlyList<string>>> Read(Stream stream)
    {
        using var zip=new ZipArchive(stream,ZipArchiveMode.Read,true);var wb=Load(zip,"xl/workbook.xml");var rels=Load(zip,"xl/_rels/workbook.xml.rels");var shared=ReadSharedStrings(zip);var relMap=rels.Root?.Elements(PackageRel+"Relationship").Where(x=>x.Attribute("Id") is not null&&x.Attribute("Target") is not null).ToDictionary(x=>x.Attribute("Id")!.Value,x=>x.Attribute("Target")!.Value)??new Dictionary<string,string>();var result=new Dictionary<string,IReadOnlyList<IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase);
        foreach(var sheet in wb.Descendants(Main+"sheet"))
        {
            var name=sheet.Attribute("name")?.Value;if(string.IsNullOrWhiteSpace(name))continue;var rid=sheet.Attribute(Rel+"id")?.Value;if(string.IsNullOrWhiteSpace(rid)||!relMap.TryGetValue(rid,out var target))continue;var path=target.StartsWith('/')?target.TrimStart('/'):$"xl/{target.TrimStart('/')}";path=Normalize(path);var entry=zip.GetEntry(path);if(entry is null)continue;using var s=entry.Open();var doc=LoadXml(s);var rows=new List<IReadOnlyList<string>>();foreach(var row in doc.Descendants(Main+"row")){var values=new List<string>();foreach(var cell in row.Elements(Main+"c")){var col=ColumnIndex(cell.Attribute("r")?.Value);while(values.Count<col)values.Add("");values.Add(CellValue(cell,shared));}rows.Add(values);}result[name]=rows;
        }
        return result;
    }

    private static string Worksheet(SpreadsheetSheetDto sheet)
    {
        var rows=new List<IReadOnlyList<string>>{sheet.Headers};rows.AddRange(sheet.Rows);var maxCols=Math.Max(1,rows.Max(r=>r.Count));var sb=new StringBuilder();sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews><cols>");for(var c=1;c<=maxCols;c++)sb.Append($"<col min=\"{c}\" max=\"{c}\" width=\"{(c==1?24:18)}\" customWidth=\"1\"/>");sb.Append("</cols><sheetData>");
        for(var r=0;r<rows.Count;r++){sb.Append($"<row r=\"{r+1}\">");for(var c=0;c<rows[r].Count;c++){var value=rows[r][c]??"";var style=r==0?1:StatusStyle(value);var cell=CellRef(c+1,r+1);sb.Append($"<c r=\"{cell}\" t=\"inlineStr\" s=\"{style}\"><is><t xml:space=\"preserve\">{Esc(value)}</t></is></c>");}sb.Append("</row>");}sb.Append("</sheetData>");if(rows.Count>0&&maxCols>0)sb.Append($"<autoFilter ref=\"A1:{ColumnName(maxCols)}{rows.Count}\"/>");sb.Append("</worksheet>");return sb.ToString();
    }
    private static int StatusStyle(string value)=>value.Trim().ToLowerInvariant() switch{"critical" or "blocked" or "overcapacity" or "failed"=>2,"atrisk" or "watch" or "nearcapacity"=>3,"healthy" or "ontrack" or "completed" or "ready"=>4,_=>0};
    private static string Styles()=>"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/><color rgb="FFFFFFFF"/></font></fonts>
  <fills count="6"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF1F4E78"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFF4CCCC"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFFE599"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFD9EAD3"/><bgColor indexed="64"/></patternFill></fill></fills>
  <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
  <cellXfs count="5"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFill="1" applyFont="1"/><xf numFmtId="0" fontId="0" fillId="3" borderId="0" xfId="0" applyFill="1"/><xf numFmtId="0" fontId="0" fillId="4" borderId="0" xfId="0" applyFill="1"/><xf numFmtId="0" fontId="0" fillId="5" borderId="0" xfId="0" applyFill="1"/></cellXfs>
  <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>
""";
    private static string ContentTypes(int count){var s=new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/><Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/><Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>");for(var i=1;i<=count;i++)s.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");s.Append("</Types>");return s.ToString();}
    private static string RootRels()=>"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/><Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/></Relationships>";
    private static string WorkbookXml(IReadOnlyList<SpreadsheetSheetDto> sheets){var sb=new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");for(var i=0;i<sheets.Count;i++)sb.Append($"<sheet name=\"{EscAttr(SafeSheetName(sheets[i].Name))}\" sheetId=\"{i+1}\" r:id=\"rId{i+1}\"/>");sb.Append("</sheets></workbook>");return sb.ToString();}
    private static string WorkbookRels(int count){var sb=new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");for(var i=1;i<=count;i++)sb.Append($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");sb.Append($"<Relationship Id=\"rId{count+1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");return sb.ToString();}
    private static string CoreProps(DateTimeOffset when)=>$"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><dc:creator>Development Intelligence Platform</dc:creator><cp:lastModifiedBy>DIP</cp:lastModifiedBy><dcterms:created xsi:type=\"dcterms:W3CDTF\">{when.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}</dcterms:created></cp:coreProperties>";
    private static string AppProps(IReadOnlyList<SpreadsheetSheetDto> sheets)=>$"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><Application>Development Intelligence Platform</Application><TitlesOfParts><vt:vector size=\"{sheets.Count}\" baseType=\"lpstr\">{string.Concat(sheets.Select(s=>$"<vt:lpstr>{Esc(SafeSheetName(s.Name))}</vt:lpstr>"))}</vt:vector></TitlesOfParts></Properties>";
    private static void Add(ZipArchive zip,string path,string content){var e=zip.CreateEntry(path,CompressionLevel.Optimal);using var s=e.Open();using var w=new StreamWriter(s,new UTF8Encoding(false));w.Write(content);}
    private static XDocument Load(ZipArchive zip,string path){var e=zip.GetEntry(path)??throw new InvalidDataException($"XLSX part '{path}' is missing.");using var s=e.Open();return LoadXml(s);}
    private static XDocument LoadXml(Stream stream){var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=32L*1024*1024};using var reader=XmlReader.Create(stream,settings);return XDocument.Load(reader,LoadOptions.None);}
    private static List<string> ReadSharedStrings(ZipArchive zip){var e=zip.GetEntry("xl/sharedStrings.xml");if(e is null)return [];using var s=e.Open();var d=LoadXml(s);return d.Descendants(Main+"si").Select(si=>string.Concat(si.Descendants(Main+"t").Select(t=>t.Value))).ToList();}
    private static string CellValue(XElement cell,IReadOnlyList<string> shared){var type=cell.Attribute("t")?.Value;if(type=="inlineStr")return string.Concat(cell.Descendants(Main+"t").Select(x=>x.Value));var value=cell.Element(Main+"v")?.Value??"";if(type=="s"&&int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var i)&&i>=0&&i<shared.Count)return shared[i];return value;}
    private static int ColumnIndex(string? cellRef){if(string.IsNullOrWhiteSpace(cellRef))return 0;var n=0;foreach(var ch in cellRef){if(!char.IsLetter(ch))break;n=n*26+(char.ToUpperInvariant(ch)-'A'+1);}return Math.Max(0,n-1);}
    private static string CellRef(int col,int row)=>ColumnName(col)+row.ToString(CultureInfo.InvariantCulture);
    private static string ColumnName(int col){var s="";while(col>0){col--;s=(char)('A'+col%26)+s;col/=26;}return s;}
    private static string SafeSheetName(string name){var clean=new string(name.Where(c=>c is not ':' and not '\\' and not '/' and not '?' and not '*' and not '[' and not ']').ToArray());if(string.IsNullOrWhiteSpace(clean))clean="Sheet";return clean[..Math.Min(31,clean.Length)];}
    private static string Esc(string s)=>System.Security.SecurityElement.Escape(s)??"";
    private static string EscAttr(string s)=>Esc(s).Replace("\"","&quot;",StringComparison.Ordinal);
    private static string Normalize(string path){var parts=new Stack<string>();foreach(var p in path.Replace('\\','/').Split('/',StringSplitOptions.RemoveEmptyEntries)){if(p=="..") {if(parts.Count>0)parts.Pop();}else if(p!=".")parts.Push(p);}return string.Join('/',parts.Reverse());}
}
