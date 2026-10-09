using System.Text;

namespace MindAttic.Export.Tests.Support;

/// <summary>What kinds of awkward content a generated string may contain.</summary>
[Flags]
public enum TextFeatures
{
    Plain = 0,
    Markup = 1,             // balanced **bold** *italic* ~~strike~~ <u>u</u>
    UnmatchedMarkers = 2,   // lone *, **, ~~, <u>, </u>
    Unicode = 4,            // em dash, curly quotes, accents, CJK
    Emoji = 8,              // astral-plane characters (surrogate pairs)
    XmlSpecials = 16,       // & < > " '
    Controls = 32,          // C0 controls inside words (never a whole line)
    EntityTags = 64,        // Prose <entity …>Name</entity> markup
    All = Markup | UnmatchedMarkers | Unicode | Emoji | XmlSpecials | Controls | EntityTags
}

/// <summary>Deterministic, seeded generator of prose-like text.</summary>
public sealed class TextGen(int seed, TextFeatures features = TextFeatures.All)
{
    public Random R { get; } = new(seed);
    public TextFeatures Features { get; } = features;

    private static readonly string[] Vocabulary =
    [
        "the", "rain", "kept", "falling", "on", "Kyle", "Mercer", "and", "nobody", "answered", "door",
        "screen", "read", "contract", "carousel", "never", "stopped", "turning", "she", "said", "his",
        "name", "wrong", "three", "times", "apartment", "smelled", "like", "camphor", "fear-sweat",
        "a", "of", "in", "to", "was", "it", "that", "with", "for", "as", "at", "by", "from", "over",
        "quietly", "ledger", "exchange", "theory", "synthetic", "value", "market", "signal", "noise",
        "5", "42", "1999", "3.14", "e.g.", "Dr.", "Mr.", "U.S.", "o'clock", "can't", "it's", "well-known"
    ];

    private static readonly string[] UnicodeWords =
    [
        "—", "–", "“quoted”", "‘single’", "…", "café", "naïve", "Straße", "façade", "résumé",
        "礼", "義", "勇", "仁", "誠", "名誉", "忠義", "Ωmega", "π≈3.14", "→", "×", "½", "€100", "№5"
    ];

    private static readonly string[] Emoji = ["🙂", "👍🏽", "🔥", "🀄", "𝔘𝔫𝔦", "🇯🇵"];

    private static readonly string[] Specials = ["&", "<", ">", "\"", "'", "a<b", "x&y", "\"q\"", "<tag>", "&amp;", "5 > 3"];

    private static readonly char[] Controls = ['\u0001', '\u0002', '\u0007', '\u0008', '\u000B', '\u000C', '\u000E', '\u001B', '\u001F'];

    private static readonly string[] Unmatched = ["*", "**", "~~", "<u>", "</u>", "* ", " *", "***"];

    private bool Has(TextFeatures f) => (Features & f) != 0;

    public bool Chance(double p) => R.NextDouble() < p;

    public T Pick<T>(IReadOnlyList<T> items) => items[R.Next(items.Count)];

    public string Word()
    {
        var roll = R.NextDouble();
        if (Has(TextFeatures.Unicode) && roll < 0.08) return Pick(UnicodeWords);
        if (Has(TextFeatures.Emoji) && roll < 0.10) return Pick(Emoji);
        if (Has(TextFeatures.XmlSpecials) && roll < 0.13) return Pick(Specials);
        var w = Pick(Vocabulary);
        if (Has(TextFeatures.Controls) && roll < 0.15 && w.Length > 1)
            w = w[..1] + Pick(Controls) + w[1..];
        return w;
    }

    public string Words(int count)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(Word());
        }
        return sb.ToString();
    }

    /// <summary>One paragraph (never contains a newline).</summary>
    public string Paragraph(int minWords = 3, int maxWords = 40)
    {
        var count = Chance(0.05) ? R.Next(200, 600) : R.Next(minWords, maxWords + 1);
        var parts = new List<string>();
        var i = 0;
        while (i < count)
        {
            var len = Math.Min(count - i, R.Next(1, 6));
            var chunk = Words(len);
            i += len;
            if (Has(TextFeatures.Markup) && Chance(0.18))
            {
                chunk = R.Next(5) switch
                {
                    0 => $"**{chunk}**",
                    1 => $"*{chunk}*",
                    2 => $"~~{chunk}~~",
                    3 => $"<u>{chunk}</u>",
                    _ => $"*{chunk} **{Words(1)}** {Words(1)}*"
                };
            }
            else if (Has(TextFeatures.UnmatchedMarkers) && Chance(0.06))
            {
                chunk = Chance(0.5) ? Pick(Unmatched) + chunk : chunk + Pick(Unmatched);
            }
            else if (Has(TextFeatures.EntityTags) && Chance(0.04))
            {
                chunk = $"<entity repo=\"character\" guid=\"{NewGuid():D}\">{chunk}</entity>";
            }
            parts.Add(chunk);
        }
        var text = string.Join(" ", parts);
        return Chance(0.5) ? text + Pick([".", "!", "?", "…", ".”", ""]) : text;
    }

    /// <summary>Several paragraphs joined the way beat text stores them.</summary>
    public string MultiParagraph(int maxParagraphs = 4)
    {
        var n = R.Next(1, maxParagraphs + 1);
        var sb = new StringBuilder();
        if (Chance(0.1)) sb.Append(Pick(["  ", "\n", " \r\n", "\t"]));
        for (var i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(Pick(["\n", "\n\n", "\r\n", "\r\n\r\n", " \n  ", "\n \n"]));
            sb.Append(Paragraph());
        }
        if (Chance(0.1)) sb.Append(Pick(["  ", "\n", "\r\n", "\n\n"]));
        return sb.ToString();
    }

    public Guid NewGuid()
    {
        var bytes = new byte[16];
        R.NextBytes(bytes);
        return new Guid(bytes);
    }

    /// <summary>A short title-like phrase.</summary>
    public string Title(int maxWords = 5)
    {
        var t = Words(R.Next(1, maxWords + 1));
        return t.Length == 0 ? "Untitled" : t;
    }

    /// <summary>A random string over a small alphabet rich in inline markers.</summary>
    public string MarkerSoup(int maxLength = 40)
    {
        string[] alphabet = ["*", "**", "~", "~~", "<u>", "</u>", "<", ">", "u", "/", "a", "b", " ", "x", "\n", "é", "🙂", "\u0007"];
        var len = R.Next(0, maxLength + 1);
        var sb = new StringBuilder();
        while (sb.Length < len) sb.Append(Pick(alphabet));
        return sb.ToString();
    }
}
