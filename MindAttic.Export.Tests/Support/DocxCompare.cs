using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MindAttic.Export.Tests.Support;

/// <summary>
/// System.IO.Packaging gives the core-properties part a random name
/// (<c>package/services/metadata/core-properties/&lt;guid&gt;.psmdcp</c>) and every package
/// relationship a random id (<c>R&lt;16 hex&gt;</c>). Those two values are the only
/// nondeterminism in a docx; both are blanked before parts are compared. Everything else (every
/// XML part, every content type) is compared verbatim, except that the children of the property
/// elements are put in schema order on both sides first: the library writes them in schema order
/// (5.0.0) and the frozen legacy writer does not, so the comparison is "same content", not "same
/// child order".
/// </summary>
public static partial class DocxCompare
{
    [GeneratedRegex(@"core-properties/[0-9a-f]{32}\.psmdcp")]
    private static partial Regex CorePropsName();

    [GeneratedRegex(@"(?<=\b(?:Id|r:id|Target)="")R[0-9a-f]{16}(?="")|(?<=\bId="")R[0-9a-f]{16}")]
    private static partial Regex RelationshipId();

    public static string PartName(string name) => CorePropsName().Replace(name, "core-properties/X.psmdcp");

    public static string Normalise(ZipEntryInfo part) =>
        SchemaOrder(RelationshipId().Replace(CorePropsName().Replace(part.Text, "core-properties/X.psmdcp"), "R*"));

    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    // CT_RPr / CT_PPrBase / CT_Settings / CT_TblBorders child order (the elements the writers use).
    private static readonly Dictionary<string, string[]> Orders = new()
    {
        ["rPr"] = ["rStyle", "rFonts", "b", "bCs", "i", "iCs", "caps", "smallCaps", "strike", "dstrike", "noProof", "webHidden",
                   "color", "spacing", "w", "kern", "position", "sz", "szCs", "highlight", "u", "effect", "vertAlign", "lang"],
        ["pPr"] = ["pStyle", "keepNext", "keepLines", "pageBreakBefore", "widowControl", "numPr", "pBdr", "shd", "tabs",
                   "spacing", "ind", "contextualSpacing", "jc", "outlineLvl", "rPr", "sectPr"],
        ["settings"] = ["mirrorMargins", "updateFields"],
        ["tblBorders"] = ["top", "start", "left", "bottom", "end", "right", "insideH", "insideV"],
        ["r"] = ["rPr"],
    };

    private static string SchemaOrder(string xml)
    {
        if (!xml.StartsWith('<') || !xml.Contains("wordprocessingml/2006/main")) return xml;
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        foreach (var el in doc.Descendants().Where(e => e.Name.Namespace == W && Orders.ContainsKey(e.Name.LocalName)).ToList())
        {
            var order = Orders[el.Name.LocalName];
            int Rank(XElement c) { var i = Array.IndexOf(order, c.Name.LocalName); return i < 0 ? order.Length : i; }
            var children = el.Elements().ToList();
            var sorted = children.Select((c, idx) => (c, idx)).OrderBy(x => Rank(x.c)).ThenBy(x => x.idx).Select(x => x.c).ToList();
            if (sorted.SequenceEqual(children)) continue;
            foreach (var c in children) c.Remove();
            foreach (var c in sorted) el.Add(c);
        }
        return doc.ToString(SaveOptions.DisableFormatting);
    }
}
