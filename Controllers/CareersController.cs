using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RebuildYou.Web.Data;
using RebuildYou.Web.Models;

namespace RebuildYou.Web.Controllers;

public class CareersController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Categories.OrderBy(c => c.SortOrder).ToListAsync());

    public async Task<IActionResult> Category(string slug, string? q, string? location, OpportunityType? type)
    {
        var cat = await db.Categories.FirstOrDefaultAsync(c => c.Slug == slug);
        if (cat is null) return NotFound();
        var query = db.Opportunities.Where(o => !o.IsDemo && o.CareerCategoryId == cat.Id && o.Status == ListingStatus.Published && o.Deadline > DateTime.Today);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(o => o.Title.Contains(q));
        if (!string.IsNullOrWhiteSpace(location)) query = query.Where(o => o.Location.Contains(location));
        if (type.HasValue) query = query.Where(o => o.Type == type);
        ViewBag.Category = cat; ViewBag.Q = q; ViewBag.Location = location; ViewBag.Type = type;
        return View(await query.OrderByDescending(o => o.DatePublished).ToListAsync());
    }
}
