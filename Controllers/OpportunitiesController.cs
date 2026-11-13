using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RebuildYou.Web.Data;
using RebuildYou.Web.Models;

namespace RebuildYou.Web.Controllers;

public class OpportunitiesController(AppDbContext db) : Controller
{
    public const int PageSize = 9;

    public async Task<IActionResult> Index(string? q, int? categoryId, string? location, OpportunityType? type, bool includeClosed = false, int page = 1)
    {
        var query = db.Opportunities.Include(o => o.Category).Where(o => o.Status == ListingStatus.Published);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(o => o.Title.Contains(q) || o.EmployerName.Contains(q));
        if (categoryId.HasValue) query = query.Where(o => o.CareerCategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(location)) query = query.Where(o => o.Location.Contains(location));
        if (type.HasValue) query = query.Where(o => o.Type == type);
        if (!includeClosed) query = query.Where(o => o.Deadline >= DateTime.Today);
        var total = await query.CountAsync();
        page = Math.Max(1, page);
        var items = await query.OrderByDescending(o => o.DatePublished).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        return View(new OppListVM
        {
            Items = items, Categories = await db.Categories.OrderBy(c => c.SortOrder).ToListAsync(),
            Q = q, CategoryId = categoryId, Location = location, Type = type, IncludeClosed = includeClosed, Page = page, Pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize))
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var o = await db.Opportunities.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id && x.Status != ListingStatus.Draft && x.Status != ListingStatus.Archived);
        if (o is null) return NotFound();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewBag.Applied = uid != null && await db.Applications.AnyAsync(a => a.OpportunityId == id && a.UserId == uid);
        ViewBag.Saved = uid != null && await db.SavedOpportunities.AnyAsync(s => s.OpportunityId == id && s.UserId == uid);
        return View(o);
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> ToggleSave(int id)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var s = await db.SavedOpportunities.FindAsync(uid, id);
        if (s is null) { if (await db.Opportunities.AnyAsync(o => o.Id == id)) db.SavedOpportunities.Add(new SavedOpportunity { UserId = uid, OpportunityId = id }); }
        else db.SavedOpportunities.Remove(s);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }
}
