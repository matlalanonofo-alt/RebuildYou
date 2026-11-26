using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RebuildYou.Web.Data;
using RebuildYou.Web.Models;
using RebuildYou.Web.Services;

namespace RebuildYou.Web.Controllers;

public record PageHit(string Title, string Summary, string Url);

public class SearchVM
{
    public string Q { get; set; } = "";
    public List<PageHit> Pages { get; set; } = new();
    public List<PageHit> Faqs { get; set; } = new();
    public List<TrainingProgramme> Programmes { get; set; } = new();
    public List<Opportunity> Opportunities { get; set; } = new();
    public List<CareerCategory> Categories { get; set; } = new();
    public string? DidYouMean { get; set; }
    public List<string> Related { get; set; } = new();
    public int Total => Pages.Count + Faqs.Count + Programmes.Count + Opportunities.Count + Categories.Count;
}

public class SearchController(AppDbContext db, IConfiguration cfg) : Controller
{
    [HttpGet, EnableRateLimiting("forms")]
    public async Task<IActionResult> Index(string? q)
    {
        q = (q ?? "").Trim();
        if (q.Length > 100) q = q[..100];
        var vm = new SearchVM { Q = q };
        var pq = SmartSearch.Parse(q);
        if (pq.IsEmpty) return View(vm);

        // The site is small, so everything searchable is loaded and ranked in memory by SmartSearch.
        var today = DateTime.Today;
        var programmes = await db.Programmes.AsNoTracking().Where(p => p.IsPublished && !p.IsArchived).Take(500).ToListAsync();
        var opportunities = await db.Opportunities.AsNoTracking().Include(o => o.Category)
            .Where(o => !o.IsDemo && o.Status == ListingStatus.Published && o.Deadline >= today).Take(500).ToListAsync();
        var categories = await db.Categories.AsNoTracking().ToListAsync();
        var faqs = SiteKnowledge.Build(cfg, Url);
        var pages = SitePages();

        vm.Programmes = SmartSearch.Rank(programmes, p => SmartSearch.Score(pq,
            (p.Title, 5.0), (p.SkillsCovered, 3.0), (p.Summary, 2.0), (p.Description, 1.0)), 8);
        vm.Opportunities = SmartSearch.Rank(opportunities, o => SmartSearch.Score(pq,
            (o.Title, 5.0), (o.Category?.Name, 3.0), (o.Skills, 3.0), (o.EmployerName, 2.0), (o.Location, 2.0), (o.ShortDescription, 1.5)), 8);
        vm.Categories = SmartSearch.Rank(categories, c => SmartSearch.Score(pq,
            (c.Name, 5.0), (c.Description, 2.0)), 8);

        var faqPage = Url.Action("Index", "Faq") ?? "/";
        vm.Faqs = SmartSearch.Rank(faqs, f => SmartSearch.Score(pq,
                (f.Question, 4.0), (string.Join(" ", f.Keywords), 3.0), (f.Answer, 1.5)), 6)
            .Select(f => new PageHit(f.Question, f.Answer.Length > 160 ? f.Answer[..157] + "..." : f.Answer, faqPage + "#faq-" + f.Id))
            .ToList();
        vm.Pages = SmartSearch.Rank(pages, p => SmartSearch.Score(pq,
                (p.Title, 5.0), (p.Keywords, 3.0), (p.Summary, 1.5)), 6)
            .Select(p => new PageHit(p.Title, p.Summary, p.Url!)).ToList();

        vm.Related = SmartSearch.RelatedWords(pq);
        if (vm.Total == 0)
        {
            var vocabulary = programmes.Select(p => p.Title + " " + p.SkillsCovered)
                .Concat(opportunities.Select(o => o.Title + " " + o.Skills))
                .Concat(categories.Select(c => c.Name))
                .Concat(pages.Select(p => p.Title + " " + p.Keywords))
                .Concat(faqs.Select(f => string.Join(" ", f.Keywords)));
            vm.DidYouMean = SmartSearch.DidYouMean(q, vocabulary);
        }
        return View(vm);
    }

    private record SitePage(string Title, string Summary, string Keywords, string? Url);

    private List<SitePage> SitePages() => new List<SitePage>
    {
        // The fixed pages of the site. Add a line here when you add a new page.
        new("Home", "Skills, work-readiness help and opportunities in one place.", "home start main", Url.Action("Index", "Home")),
        new("Training programmes", "Practical, skills-based programmes you can register your interest in.", "training programmes courses learn skills study", Url.Action("Index", "Training")),
        new("Opportunities", "Jobs, learnerships, internships and more.", "opportunities jobs vacancies learnership internship work apply", Url.Action("Index", "Opportunities")),
        new("Career categories", "Browse opportunities by career area.", "career careers categories industries", Url.Action("Index", "Careers")),
        new("Get Work-Ready", "Free tips on your CV, interviews and your first weeks at work.", "work ready cv resume interview tips advice first job", Url.Action("Index", "Resources")),
        new("For Employers", "Share vacancies, source candidates or enquire about staff training.", "employers hire recruit vacancy staff training companies", Url.Action("Employers", "Home")),
        new("About Us", "Our story, vision, mission, values, services and leadership.", "about us story vision mission values services leadership accreditation ceo founder", Url.Action("About", "Home")),
        new("FAQ and Help", "Answers to common questions and an assistant that can help.", "faq help questions answers chatbot assistant support fees dates requirements", Url.Action("Index", "Faq")),
        new("Contact Us", "Our address, phone, email and contact form.", "contact phone email address location enquiry message whatsapp", Url.Action("Contact", "Home")),
        new("Privacy Notice", "How we collect, use and protect your information.", "privacy popia terms data personal information rights", Url.Action("Privacy", "Home")),
        new("Log in", "Sign in to your account.", "login log in sign in account", Url.Action("Login", "Account")),
        new("Create a free profile", "Register for a free account.", "register sign up create account profile free", Url.Action("Register", "Account")),
    }.Where(p => p.Url != null).ToList();
}
