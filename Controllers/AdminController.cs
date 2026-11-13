using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RebuildYou.Web.Data;
using RebuildYou.Web.Models;

namespace RebuildYou.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(AppDbContext db, UserManager<ApplicationUser> users, IWebHostEnvironment env) : Controller
{
    string Me => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    async Task Log(string action, string? details = null) { db.AuditLogs.Add(new AuditLog { ActorId = Me, Action = action, Details = details }); await db.SaveChangesAsync(); }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var vm = new AdminStatsVM
        {
            Users = await db.Users.CountAsync(), ActiveUsers = await db.Users.CountAsync(u => u.IsActive),
            Cvs = await db.CvDocuments.CountAsync(),
            Published = await db.Opportunities.CountAsync(o => o.Status == ListingStatus.Published),
            Open = await db.Opportunities.CountAsync(o => o.Status == ListingStatus.Published && o.Deadline >= today),
            Expired = await db.Opportunities.CountAsync(o => o.Status == ListingStatus.Published && o.Deadline < today),
            Applications = await db.Applications.CountAsync(),
            AwaitingReview = await db.Applications.CountAsync(a => a.Status == ApplicationStatus.Submitted),
            Shortlisted = await db.Applications.CountAsync(a => a.Status == ApplicationStatus.Shortlisted),
            Programmes = await db.Programmes.CountAsync(p => p.IsPublished && !p.IsArchived),
            Enquiries = await db.Enquiries.CountAsync(e => !e.IsHandled),
            LoginsToday = await db.LoginRecords.CountAsync(l => l.Event == "Login" && l.At >= DateTime.UtcNow.Date),
            Recent = await db.AuditLogs.OrderByDescending(a => a.At).Take(8).ToListAsync()
        };
        vm.ByCategory = (await db.Applications.GroupBy(a => a.Opportunity!.Category!.Name).Select(g => new { g.Key, C = g.Count() }).ToListAsync()).Select(x => (x.Key, x.C)).ToList();
        vm.ByStatus = (await db.Applications.GroupBy(a => a.Status).Select(g => new { g.Key, C = g.Count() }).ToListAsync()).Select(x => (x.Key.ToString(), x.C)).ToList();
        return View(vm);
    }

    // ---------- Users ----------
    public async Task<IActionResult> Users(string? q, string? interest)
    {
        var query = db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(u => u.Email!.Contains(q) || u.FirstName.Contains(q) || u.Surname.Contains(q) || u.PhoneNumber!.Contains(q));
        if (!string.IsNullOrWhiteSpace(interest)) query = query.Where(u => u.CareerInterests!.Contains(interest));
        ViewBag.Q = q; ViewBag.Interest = interest;
        ViewBag.AdminIds = (await users.GetUsersInRoleAsync("Admin")).Select(x => x.Id).ToHashSet();
        return View(await query.OrderByDescending(u => u.CreatedAt).Take(300).ToListAsync());
    }

    public async Task<IActionResult> UserDetail(string id)
    {
        var u = await users.FindByIdAsync(id); if (u is null) return NotFound();
        await Log("Viewed user profile", u.Email);
        return View(new UserDetailVM
        {
            User = u, IsAdmin = await users.IsInRoleAsync(u, "Admin"),
            Cvs = await db.CvDocuments.Where(c => c.UserId == id).OrderByDescending(c => c.UploadedAt).ToListAsync(),
            Applications = await db.Applications.Include(a => a.Opportunity).Where(a => a.UserId == id).OrderByDescending(a => a.SubmittedAt).ToListAsync(),
            Interests = await db.ProgrammeInterests.Include(i => i.Programme).Where(i => i.UserId == id).ToListAsync(),
            Logins = await db.LoginRecords.Where(l => l.UserId == id).OrderByDescending(l => l.At).Take(30).ToListAsync()
        });
    }

    public async Task<IActionResult> LoginActivity(string? q, string? ev)
    {
        var query = db.LoginRecords.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(l => l.Email.Contains(q));
        if (!string.IsNullOrWhiteSpace(ev)) query = query.Where(l => l.Event == ev);
        ViewBag.Q = q; ViewBag.Ev = ev;
        return View(await query.OrderByDescending(l => l.At).Take(300).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> ToggleAdmin(string id)
    {
        var u = await users.FindByIdAsync(id);
        if (u is null || u.Id == Me) return RedirectToAction(nameof(Users));
        if (await users.IsInRoleAsync(u, "Admin")) { await users.RemoveFromRoleAsync(u, "Admin"); await Log("Removed admin access", u.Email); TempData["Success"] = u.Email + " is no longer an administrator."; }
        else { await users.AddToRoleAsync(u, "Admin"); await Log("Granted admin access", u.Email); TempData["Success"] = u.Email + " is now an administrator."; }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleUser(string id)
    {
        var u = await users.FindByIdAsync(id);
        if (u is null || u.Id == Me) return RedirectToAction(nameof(Users));
        u.IsActive = !u.IsActive; await users.UpdateAsync(u);
        await Log(u.IsActive ? "Reactivated user" : "Suspended user", u.Email);
        return RedirectToAction(nameof(Users));
    }

    // ---------- Opportunities ----------
    public async Task<IActionResult> Opportunities() => View(await db.Opportunities.Include(o => o.Category).OrderByDescending(o => o.DatePublished).ToListAsync());

    [HttpGet] public async Task<IActionResult> OpportunityForm(int? id)
    {
        ViewBag.Categories = await db.Categories.OrderBy(c => c.SortOrder).ToListAsync();
        return View(id is null ? new Opportunity() : await db.Opportunities.FindAsync(id.Value));
    }

    [HttpPost]
    public async Task<IActionResult> OpportunityForm(Opportunity m)
    {
        ModelState.Remove(nameof(Opportunity.Category));
        if (!ModelState.IsValid) { ViewBag.Categories = await db.Categories.OrderBy(c => c.SortOrder).ToListAsync(); return View(m); }
        if (m.Id == 0) { m.DatePublished = DateTime.UtcNow; db.Opportunities.Add(m); }
        else
        {
            var o = await db.Opportunities.FindAsync(m.Id); if (o is null) return NotFound();
            o.Title = m.Title; o.EmployerName = m.EmployerName; o.CareerCategoryId = m.CareerCategoryId; o.Location = m.Location; o.Type = m.Type;
            o.Salary = m.Salary; o.Qualifications = m.Qualifications; o.Experience = m.Experience; o.ShortDescription = m.ShortDescription;
            o.Responsibilities = m.Responsibilities; o.Skills = m.Skills; o.ApplicationInstructions = m.ApplicationInstructions;
            o.DocumentsRequired = m.DocumentsRequired; o.Deadline = m.Deadline; o.Status = m.Status; o.ExternalUrl = m.ExternalUrl; o.IsDemo = m.IsDemo;
        }
        await db.SaveChangesAsync();
        await Log(m.Id == 0 ? "Created opportunity" : "Edited opportunity", m.Title);
        TempData["Success"] = "Opportunity saved.";
        return RedirectToAction(nameof(Opportunities));
    }

    [HttpPost]
    public async Task<IActionResult> SetOpportunityStatus(int id, ListingStatus status)
    {
        if (!ModelState.IsValid) return RedirectToAction(nameof(Opportunities));
        var o = await db.Opportunities.FindAsync(id); if (o is null) return NotFound();
        o.Status = status; await db.SaveChangesAsync(); await Log($"Opportunity set to {status}", o.Title);
        return RedirectToAction(nameof(Opportunities));
    }

    // ---------- Applications ----------
    public async Task<IActionResult> Applications(ApplicationStatus? status, int? opportunityId)
    {
        var q = db.Applications.Include(a => a.Opportunity).Include(a => a.User).Include(a => a.Cv).AsQueryable();
        if (status.HasValue) q = q.Where(a => a.Status == status);
        if (opportunityId.HasValue) q = q.Where(a => a.OpportunityId == opportunityId);
        ViewBag.Status = status;
        return View(await q.OrderByDescending(a => a.SubmittedAt).Take(300).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> UpdateApplication(int id, ApplicationStatus status, DateTime? interviewDate, string? notes)
    {
        var a = await db.Applications.Include(x => x.Opportunity).FirstOrDefaultAsync(x => x.Id == id); if (a is null) return NotFound();
        var old = a.Status; a.Status = status; a.InterviewDate = interviewDate; a.InternalNotes = notes;
        await db.SaveChangesAsync();
        await Log("Application status changed", $"Application {id} ({a.Opportunity?.Title}): {old} -> {status}");
        TempData["Success"] = "Application updated.";
        return RedirectToAction(nameof(Applications));
    }

    public async Task<IActionResult> ViewCv(int id)
    {
        var cv = await db.CvDocuments.FindAsync(id); if (cv is null) return NotFound();
        await Log("Admin viewed CV", $"CV {id} of user {cv.UserId}");   // sensitive access is always logged
        var path = Path.Combine(env.ContentRootPath, "App_Data", "cvs", cv.StoredFileName);
        return System.IO.File.Exists(path) ? PhysicalFile(path, cv.ContentType, cv.OriginalFileName) : NotFound();
    }

    public async Task<IActionResult> ExportApplications()
    {
        var rows = await db.Applications.Include(a => a.Opportunity).OrderBy(a => a.SubmittedAt).ToListAsync();
        var sb = new StringBuilder("ApplicationId,Opportunity,Status,SubmittedAt\n");
        foreach (var r in rows)
        {
            var title = (r.Opportunity?.Title ?? "").Replace("\"", "\"\"");
            sb.AppendLine(r.Id + ",\"" + title + "\"," + r.Status + "," + r.SubmittedAt.ToString("yyyy-MM-dd"));
        }
        await Log("Exported applications report");   // no personal details are exported
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "applications-report.csv");
    }

    // ---------- Training programmes ----------
    public async Task<IActionResult> Programmes() => View(await db.Programmes.OrderBy(p => p.SortOrder).ToListAsync());

    [HttpGet] public async Task<IActionResult> ProgrammeForm(int? id) => View(id is null ? new TrainingProgramme() : await db.Programmes.FindAsync(id.Value));

    [HttpPost]
    public async Task<IActionResult> ProgrammeForm(TrainingProgramme m)
    {
        if (!ModelState.IsValid) return View(m);
        if (m.Id == 0) db.Programmes.Add(m); else db.Programmes.Update(m);
        await db.SaveChangesAsync(); await Log("Saved programme", m.Title);
        return RedirectToAction(nameof(Programmes));
    }

    [HttpPost]
    public async Task<IActionResult> ProgrammeState(int id, string op)
    {
        var p = await db.Programmes.FindAsync(id); if (p is null) return NotFound();
        if (op == "publish") p.IsPublished = true; else if (op == "unpublish") p.IsPublished = false; else if (op == "archive") { p.IsArchived = true; p.IsPublished = false; } else if (op == "restore") p.IsArchived = false;
        await db.SaveChangesAsync(); await Log("Programme " + op, p.Title);
        return RedirectToAction(nameof(Programmes));
    }

    // ---------- Categories, enquiries ----------
    public async Task<IActionResult> Categories() => View(await db.Categories.OrderBy(c => c.SortOrder).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> AddCategory(string name, string description)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(description))
        {
            var slug = new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
            if (!await db.Categories.AnyAsync(c => c.Slug == slug)) { db.Categories.Add(new CareerCategory { Name = name.Trim(), Slug = slug, Description = description.Trim(), SortOrder = 99 }); await db.SaveChangesAsync(); await Log("Added category", name); }
        }
        return RedirectToAction(nameof(Categories));
    }

    public async Task<IActionResult> Interest() =>
        View(await db.ProgrammeInterests.Include(i => i.User).Include(i => i.Programme).OrderByDescending(i => i.At).Take(300).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var u = await users.FindByIdAsync(id); if (u is null) return NotFound();
        var temp = "Rb" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)) + "a1";
        var res = await users.ResetPasswordAsync(u, await users.GeneratePasswordResetTokenAsync(u), temp);
        if (res.Succeeded) { await Log("Reset user password", u.Email); TempData["Success"] = "Temporary password for " + u.Email + ": " + temp + " . Give it to the user privately and ask them to change it after logging in."; }
        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> Enquiries() => View(await db.Enquiries.OrderByDescending(e => e.CreatedAt).Take(200).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> HandleEnquiry(int id)
    {
        var e = await db.Enquiries.FindAsync(id); if (e is not null) { e.IsHandled = true; await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Enquiries));
    }
}
