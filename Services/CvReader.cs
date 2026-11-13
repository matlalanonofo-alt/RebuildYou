using System.IO.Compression;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace RebuildYou.Web.Services;

public record CvInfo(string? City, string? Education, string? Interests);

public static class CvReader
{
    static readonly string[] Cities = { "Johannesburg", "Roodepoort", "Soweto", "Pretoria", "Durban", "Cape Town", "Randburg", "Sandton", "Krugersdorp", "Centurion", "Germiston", "Benoni" };
    static readonly (string Name, string Pattern)[] Fields =
    {
        ("Hospitality", "hospitality|waiter|waitress|kitchen|housekeeping|hotel"),
        ("Call Centre & Customer Service", "call cent|contact cent|customer service|customer support"),
        ("Reception & Administration", "reception|administrat|data captur|filing"),
        ("Computer Literacy & Digital Skills", "computer|microsoft (word|excel)|excel|data entry|it support")
    };

    // Best effort only. Never throws, never overwrites what the user typed.
    public static CvInfo Read(string path, string ext)
    {
        try
        {
            string text;
            if (ext == ".pdf")
            {
                using var d = PdfDocument.Open(path);
                text = string.Join(" ", d.GetPages().Take(3).Select(p => p.Text));
            }
            else
            {
                using var z = ZipFile.OpenRead(path);
                var e = z.GetEntry("word/document.xml");
                if (e == null) return new CvInfo(null, null, null);
                using var r = new StreamReader(e.Open());
                text = Regex.Replace(r.ReadToEnd(), "<[^>]+>", " ");
            }
            var city = Cities.FirstOrDefault(c => text.Contains(c, StringComparison.OrdinalIgnoreCase));
            string? edu = null;
            if (Regex.IsMatch(text, @"\b(degree|bachelor|bcom|bsc|honours)\b", RegexOptions.IgnoreCase)) edu = "Degree";
            else if (Regex.IsMatch(text, @"\b(diploma|certificate|n4|n5|n6)\b", RegexOptions.IgnoreCase)) edu = "Certificate / Diploma";
            else if (Regex.IsMatch(text, @"\b(matric|grade 12|nsc)\b", RegexOptions.IgnoreCase)) edu = "Grade 12 / Matric";
            var found = Fields.Where(f => Regex.IsMatch(text, f.Pattern, RegexOptions.IgnoreCase)).Select(f => f.Name).Take(3).ToList();
            return new CvInfo(city, edu, found.Count > 0 ? string.Join(", ", found) : null);
        }
        catch { return new CvInfo(null, null, null); }
    }
}
