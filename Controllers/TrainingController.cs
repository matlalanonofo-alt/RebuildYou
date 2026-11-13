using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RebuildYou.Web.Data;
using RebuildYou.Web.Models;

namespace RebuildYou.Web.Controllers;

public class TrainingController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Programmes.Where(p => p.IsPublished && !p.IsArchived).OrderBy(p => p.SortOrder).ToListAsync());

    public async Task<IActionResult> Details(string slug)
    {
        var p = await db.Programmes.FirstOrDefaultAsync(x => x.Slug == slug && x.IsPublished && !x.IsArchived);
        if (p is null) return NotFound();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewBag.Interested = uid != null && await db.ProgrammeInterests.AnyAsync(i => i.UserId == uid && i.ProgrammeId == p.Id);
        var today = DateTime.Today;
        ViewBag.Related = string.IsNullOrEmpty(p.RelatedCategorySlug) ? new List<Opportunity>() :
            await db.Opportunities.Include(o => o.Category).Where(o => o.Category!.Slug == p.RelatedCategorySlug && o.Status == ListingStatus.Published && o.Deadline >= today).Take(3).ToListAsync();
        return View(p);
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> RegisterInterest(int id)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var p = await db.Programmes.FindAsync(id);
        if (p is null) return NotFound();
        if (!await db.ProgrammeInterests.AnyAsync(i => i.UserId == uid && i.ProgrammeId == id))
        {
            db.ProgrammeInterests.Add(new ProgrammeInterest { UserId = uid, ProgrammeId = id });
            await db.SaveChangesAsync();
        }
        TempData["Success"] = "Thank you! RebuildYOU can now see your interest in " + p.Title + " and will use your profile details to contact you about dates.";
        return RedirectToAction(nameof(Details), new { slug = p.Slug });
    }
}
