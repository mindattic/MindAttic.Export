using System.Text.RegularExpressions;

namespace MindAttic.Export.Tests.Support;

/// <summary>
/// System.IO.Packaging gives the core-properties part a random name
/// (<c>package/services/metadata/core-properties/&lt;guid&gt;.psmdcp</c>) and every package
/// relationship a random id (<c>R&lt;16 hex&gt;</c>). Those two values are the only
/// nondeterminism in a docx; both are blanked before parts are compared. Everything else (every
/// XML part, every content type) is compared verbatim.
/// </summary>
public static partial class DocxCompare
{
    [GeneratedRegex(@"core-properties/[0-9a-f]{32}\.psmdcp")]
    private static partial Regex CorePropsName();

    [GeneratedRegex(@"(?<=\b(?:Id|r:id|Target)="")R[0-9a-f]{16}(?="")|(?<=\bId="")R[0-9a-f]{16}")]
    private static partial Regex RelationshipId();

    public static string PartName(string name) => CorePropsName().Replace(name, "core-properties/X.psmdcp");

    public static string Normalise(ZipEntryInfo part) =>
        RelationshipId().Replace(CorePropsName().Replace(part.Text, "core-properties/X.psmdcp"), "R*");
}
