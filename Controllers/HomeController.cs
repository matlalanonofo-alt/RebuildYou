using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RebuildYou.Web.Data;
using RebuildYou.Web.Models;

namespace RebuildYou.Web.Controllers;

public class HomeController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        return View(new HomeVM
        {
            Programmes = await db.Programmes.Where(p => p.IsPublished && !p.IsArchived).OrderBy(p => p.SortOrder).ToListAsync(),
            Categories = await db.Categories.OrderBy(c => c.SortOrder).ToListAsync(),
            Latest = await db.Opportunities.Include(o => o.Category)
                .Where(o => o.Status == ListingStatus.Published && o.Deadline >= today)
                .OrderByDescending(o => o.DatePublished).Take(3).ToListAsync()
        });
    }
    public IActionResult About() => View();
    public IActionResult Privacy() => View();
    [HttpGet] public IActionResult Contact(string? subject) => View(new ContactVM { Subject = subject ?? "" });
    public IActionResult Employers() => View();

    [HttpPost, EnableRateLimiting("forms")]
    public async Task<IActionResult> Contact(ContactVM m)
    {
        if (!string.IsNullOrEmpty(m.Website)) return RedirectToAction(nameof(Contact)); // bot trap
        if (!ModelState.IsValid) return View(m);
        try
        {
            db.Enquiries.Add(new ContactEnquiry { Name = m.Name.Trim(), Email = m.Email.Trim(), Subject = m.Subject.Trim(), Message = m.Message.Trim() });
            await db.SaveChangesAsync();
            TempData["Success"] = "Thank you. Your message has been received and we will respond soon.";
            return RedirectToAction(nameof(Contact));
        }
        catch
        {
            ModelState.AddModelError("", "We could not send your message. Please try again or phone us.");
            return View(m);
        }
    }
    [ResponseCache(NoStore = true)] public IActionResult Error() => View();
}
