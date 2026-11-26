using System.Text;

namespace RebuildYou.Web.Services;

public sealed class QueryTerm
{
    public string Stem { get; init; } = "";
    /// <summary>Stems of words that mean (nearly) the same thing, e.g. "vacancy" for "job".</summary>
    public HashSet<string> Related { get; init; } = new();
}

public sealed class ParsedQuery
{
    public List<QueryTerm> Terms { get; } = new();
    public List<string> Raw { get; } = new();
    public bool IsEmpty => Terms.Count == 0;
}

/// <summary>
/// Smart site search without any outside service: it understands word families (job = vacancy = work),
/// forgives typing mistakes, matches word beginnings, and ranks results by how well and where they match.
/// To teach it new words, add them to a group in <see cref="Groups"/>.
/// </summary>
public static class SmartSearch
{
    private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
    {
        "a","an","the","and","or","of","to","for","in","on","at","by","with","from","is","are","am","was","be",
        "do","does","did","can","could","would","should","will","i","me","my","we","our","you","your","it","its",
        "how","what","which","who","where","when","why","there","this","that","any","about","please","want","need","like","get"
    };

    /// <summary>Groups of words that mean about the same thing. Add words freely; they only help.</summary>
    private static readonly string[][] Groups =
    {
        new[] { "job", "jobs", "vacancy", "vacancies", "work", "employment", "career", "careers", "position", "role", "hiring", "opening", "opportunity", "opportunities" },
        new[] { "learnership", "learnerships", "internship", "internships", "intern", "apprenticeship", "trainee", "graduate" },
        new[] { "course", "courses", "training", "programme", "programmes", "program", "programs", "class", "classes", "learn", "study", "skills", "skill", "workshop", "certificate" },
        new[] { "cv", "resume", "curriculum", "vitae" },
        new[] { "interview", "interviews" },
        new[] { "fee", "fees", "price", "prices", "cost", "costs", "tuition", "payment", "afford", "cheap", "quote" },
        new[] { "contact", "phone", "call", "telephone", "email", "whatsapp", "message", "reach", "number" },
        new[] { "address", "location", "directions", "map", "office", "visit", "roodepoort", "johannesburg" },
        new[] { "register", "registration", "signup", "enrol", "enroll", "enrolment", "join", "apply", "application", "account", "profile" },
        new[] { "login", "signin", "password", "forgot", "reset", "locked" },
        new[] { "employer", "employers", "company", "companies", "business", "recruit", "recruiter", "hire", "staff" },
        new[] { "computer", "computers", "digital", "typing", "excel", "software", "technology", "tech", "it" },
        new[] { "customer", "service", "reception", "receptionist", "admin", "administration", "support", "callcentre" },
        new[] { "privacy", "popia", "data", "personal", "information", "protect", "delete", "consent" },
        new[] { "about", "story", "mission", "vision", "values", "founder", "ceo", "leader", "leadership" },
        new[] { "hours", "times", "schedule", "dates", "start", "begin", "duration" },
        new[] { "accredited", "accreditation", "certified", "recognised", "recognized", "qualification", "qualifications", "seta" },
        new[] { "help", "assist", "assistance", "faq", "question", "questions", "guide", "advice", "tips" },
    };

    private static readonly List<HashSet<string>> GroupStems = new();
    private static readonly Dictionary<string, List<int>> GroupOf = new(StringComparer.Ordinal);

    static SmartSearch()
    {
        for (var i = 0; i < Groups.Length; i++)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var word in Groups[i])
            {
                var stem = Stem(word);
                set.Add(stem);
                if (!GroupOf.TryGetValue(stem, out var ids)) GroupOf[stem] = ids = new List<int>();
                if (!ids.Contains(i)) ids.Add(i);
            }
            GroupStems.Add(set);
        }
    }

    /// <summary>Every word the search knows about from the word groups (used for "Did you mean").</summary>
    public static IEnumerable<string> GroupWords => Groups.SelectMany(g => g);

    public static List<string> Tokens(string? text)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        var sb = new StringBuilder();
        foreach (var ch in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); continue; }
            if (sb.Length > 0) { list.Add(sb.ToString()); sb.Clear(); }
        }
        if (sb.Length > 0) list.Add(sb.ToString());
        return list;
    }

    /// <summary>Light English stemmer so "courses", "course" and "learning" / "learn" line up.</summary>
    public static string Stem(string w)
    {
        if (w.Length > 5 && w.EndsWith("ing", StringComparison.Ordinal)) return w[..^3];
        if (w.Length > 4 && w.EndsWith("ies", StringComparison.Ordinal)) return w[..^3] + "y";
        if (w.Length > 4 && w.EndsWith("es", StringComparison.Ordinal)) return w[..^2];
        if (w.Length > 3 && w.EndsWith("s", StringComparison.Ordinal) && !w.EndsWith("ss", StringComparison.Ordinal)) return w[..^1];
        if (w.Length > 4 && w.EndsWith("ed", StringComparison.Ordinal)) return w[..^2];
        return w;
    }

    public static ParsedQuery Parse(string? q)
    {
        var pq = Build(q, true);
        return pq.IsEmpty ? Build(q, false) : pq;   // a query made only of small words (like "how") still searches
    }

    private static ParsedQuery Build(string? q, bool dropSmallWords)
    {
        var pq = new ParsedQuery();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in Tokens(q))
        {
            if (pq.Terms.Count >= 6) break;
            if (raw.Length < 2 || (dropSmallWords && Stop.Contains(raw))) continue;
            var stem = Stem(raw);
            if (!seen.Add(stem)) continue;
            var term = new QueryTerm { Stem = stem };
            if (GroupOf.TryGetValue(stem, out var ids))
                foreach (var id in ids)
                    foreach (var w in GroupStems[id])
                        if (w != stem) term.Related.Add(w);
            pq.Terms.Add(term);
            pq.Raw.Add(raw);
        }
        return pq;
    }

    /// <summary>Other words the search also looked for, shown to the visitor ("Also searched: vacancy, work...").</summary>
    public static List<string> RelatedWords(ParsedQuery q, int max = 8)
    {
        var result = new List<string>();
        foreach (var term in q.Terms)
        {
            if (!GroupOf.TryGetValue(term.Stem, out var ids)) continue;
            foreach (var id in ids)
                foreach (var word in Groups[id])
                    if (Stem(word) != term.Stem && !q.Raw.Contains(word) && !result.Contains(word))
                        result.Add(word);
        }
        return result.Take(max).ToList();
    }

    private static double MatchValue(QueryTerm t, string d)
    {
        var s = t.Stem;
        if (d == s) return 1.0;                                       // same word
        if (t.Related.Contains(d)) return 0.65;                       // same idea (job / vacancy)
        if (s.Length >= 3 && d.Length > s.Length && d.StartsWith(s, StringComparison.Ordinal)) return 0.85;   // word beginning (comput -> computer)
        var max = s.Length >= 8 ? 2 : s.Length >= 4 ? 1 : 0;
        if (max > 0 && d.Length >= 4 && Math.Abs(d.Length - s.Length) <= max && (s[0] == d[0] || s[1] == d[1]) && Distance(s, d, max) <= max)
            return 0.7;                                               // typing mistake (intrnship -> internship)
        return 0;
    }

    /// <summary>
    /// Scores one item from its text fields (each with an importance weight). 0 means "not a match".
    /// Titles count more than descriptions; items matching more of the visitor's words rank higher.
    /// </summary>
    public static double Score(ParsedQuery q, params (string? Text, double Weight)[] fields)
    {
        if (q.IsEmpty) return 0;
        var tokenised = new List<(List<string> Stems, double Weight)>(fields.Length);
        foreach (var f in fields)
            tokenised.Add((Tokens(f.Text).Select(Stem).ToList(), f.Weight));

        double total = 0;
        var matched = 0;
        foreach (var t in q.Terms)
        {
            double best = 0;
            foreach (var (stems, weight) in tokenised)
                foreach (var d in stems)
                {
                    var m = MatchValue(t, d) * weight;
                    if (m > best) best = m;
                }
            if (best > 0) { total += best; matched++; }
        }
        if (matched == 0) return 0;
        var coverage = (double)matched / q.Terms.Count;
        if (q.Terms.Count > 1 && coverage < 0.5) return 0;
        return total * (0.5 + 0.5 * coverage);
    }

    /// <summary>Best matches first; weak matches (well below the top score) are dropped.</summary>
    public static List<T> Rank<T>(IEnumerable<T> items, Func<T, double> score, int take)
    {
        var scored = items.Select(i => (Item: i, Score: score(i))).Where(x => x.Score > 0)
                          .OrderByDescending(x => x.Score).ToList();
        if (scored.Count == 0) return new List<T>();
        var cut = scored[0].Score * 0.35;
        return scored.Where(x => x.Score >= cut).Take(take).Select(x => x.Item).ToList();
    }

    /// <summary>"Did you mean ...?" when a word looks like a typing mistake of a word used on the site.</summary>
    public static string? DidYouMean(string? q, IEnumerable<string> vocabulary)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in vocabulary.Concat(GroupWords))
            foreach (var t in Tokens(v))
                if (t.Length >= 4) known.Add(t);

        var parts = new List<string>();
        var changed = false;
        foreach (var raw in Tokens(q))
        {
            if (raw.Length < 4 || Stop.Contains(raw) || known.Contains(raw)) { parts.Add(raw); continue; }
            string? bestWord = null;
            var bestDist = 99;
            foreach (var v in known)
            {
                if (Math.Abs(v.Length - raw.Length) > 2) continue;
                var d = Distance(raw, v, 2);
                if (d < bestDist) { bestDist = d; bestWord = v; }
            }
            var allowed = raw.Length >= 8 ? 2 : 1;
            if (bestWord != null && bestDist <= allowed) { parts.Add(bestWord); changed = true; }
            else parts.Add(raw);
        }
        return changed ? string.Join(' ', parts) : null;
    }

    /// <summary>Edit distance (insert, delete, change, swap neighbours). Anything over <paramref name="max"/> returns max + 1 or more.</summary>
    private static int Distance(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max) return max + 1;
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        return d[a.Length, b.Length];
    }
}
