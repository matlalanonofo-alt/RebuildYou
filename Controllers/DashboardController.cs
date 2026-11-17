using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using RebuildYou.Web.Data;
using RebuildYou.Web.Models;
using RebuildYou.Web.Services;

namespace RebuildYou.Web.Controllers;

[Authorize]
public class DashboardController(AppDbContext db, UserManager<ApplicationUser> users, IWebHostEnvironment env) : Controller
{
    const long MaxCvBytes = 5 * 1024 * 1024;
    const long MaxSupportingDocumentBytes = 5 * 1024 * 1024;
    string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    string CvFolder => Path.Combine(env.ContentRootPath, "App_Data", "cvs"); // outside wwwroot: never publicly served
    string ApplicationFilesFolder => Path.Combine(env.ContentRootPath, "App_Data", "applications");

    public async Task<IActionResult> Index()
    {
        var u = (await users.GetUserAsync(User))!;
        var cv = await db.CvDocuments.Where(c => c.UserId == Uid).OrderByDescending(c => c.UploadedAt).FirstOrDefaultAsync();
        var fields = new[] { u.FirstName, u.Surname, u.PhoneNumber, u.City, u.EducationLevel, u.EmploymentStatus, u.CareerInterests, u.TrainingInterests };
        var pct = (int)Math.Round((fields.Count(f => !string.IsNullOrWhiteSpace(f)) + (cv != null ? 1 : 0)) * 100.0 / (fields.Length + 1));
        var open = db.Opportunities.Include(o => o.Category).Where(o => o.Status == ListingStatus.Published && o.Deadline > DateTime.Today);
        var interest = (u.CareerInterests ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var openList = await open.OrderByDescending(o => o.DatePublished).Take(100).ToListAsync();
        var rec = interest.Length == 0 ? new List<Opportunity>() : openList.Where(o => interest.Any(i =>
            (o.Category?.Name ?? "").Contains(i, StringComparison.OrdinalIgnoreCase) || o.Title.Contains(i, StringComparison.OrdinalIgnoreCase))).Take(3).ToList();
        if (rec.Count == 0) rec = openList.Take(3).ToList();
        return View(new DashboardVM
        {
            User = u, Completion = pct, Cv = cv, Recommended = rec,
            Applications = await db.Applications.Include(a => a.Opportunity).Where(a => a.UserId == Uid).OrderByDescending(a => a.SubmittedAt).Take(5).ToListAsync(),
            Saved = await db.Opportunities.Include(o => o.Category).Where(o => db.SavedOpportunities.Any(s => s.UserId == Uid && s.OpportunityId == o.Id)).Take(5).ToListAsync(),
            Programmes = await db.Programmes.Where(p => p.IsPublished && !p.IsArchived).OrderBy(p => p.SortOrder).Take(3).ToListAsync()
        });
    }

    // ---------- CV management ----------
    public async Task<IActionResult> Cv() => View(await db.CvDocuments.Where(c => c.UserId == Uid).OrderByDescending(c => c.UploadedAt).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> UploadCv(IFormFile? file)
    {
        if (file is null || file.Length == 0) { TempData["Error"] = "Please select a file."; return RedirectToAction(nameof(Cv)); }
        if (file.Length > MaxCvBytes) { TempData["Error"] = "The file is larger than 5 MB."; return RedirectToAction(nameof(Cv)); }
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".pdf" or ".docx")) { TempData["Error"] = "Only PDF and DOCX files are accepted."; return RedirectToAction(nameof(Cv)); }

        // verify real content (magic bytes), not just the extension
        var head = new byte[4];
        await using (var s = file.OpenReadStream()) { await s.ReadExactlyAsync(head, 0, 4); }
        var okPdf = ext == ".pdf" && head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46;      // %PDF
        var okDocx = ext == ".docx" && head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04;    // PK.. (zip)
        if (!okPdf && !okDocx) { TempData["Error"] = "That file does not look like a valid PDF or DOCX."; return RedirectToAction(nameof(Cv)); }

        Directory.CreateDirectory(CvFolder);
        var stored = Guid.NewGuid().ToString("N") + ext;      // random name; original name is never used on disk
        await using (var fs = System.IO.File.Create(Path.Combine(CvFolder, stored))) { await file.CopyToAsync(fs); }
        var safeName = Path.GetFileName(file.FileName); if (safeName.Length > 200) safeName = safeName[^200..];
        db.CvDocuments.Add(new CvDocument { UserId = Uid, OriginalFileName = safeName, StoredFileName = stored, ContentType = ext == ".pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document", SizeBytes = file.Length });
        await db.SaveChangesAsync();
        var info = CvReader.Read(Path.Combine(CvFolder, stored), ext);
        var me = (await users.GetUserAsync(User))!; var filled = new List<string>();
        if (string.IsNullOrWhiteSpace(me.City) && info.City != null) { me.City = info.City; filled.Add("area"); }
        if (string.IsNullOrWhiteSpace(me.EducationLevel) && info.Education != null) { me.EducationLevel = info.Education; filled.Add("education level"); }
        if (string.IsNullOrWhiteSpace(me.CareerInterests) && info.Interests != null) { me.CareerInterests = info.Interests; filled.Add("career interests"); }
        if (filled.Count > 0) await users.UpdateAsync(me);
        TempData["Success"] = filled.Count > 0 ? "Your CV is uploaded. We filled in your " + string.Join(", ", filled) + " from it. Please check your profile." : "Your CV has been uploaded.";
        return RedirectToAction(nameof(Cv));
    }

    public async Task<IActionResult> DownloadCv(int id)
    {
        var cv = await db.CvDocuments.FirstOrDefaultAsync(c => c.Id == id && c.UserId == Uid);   // ownership enforced
        if (cv is null) return NotFound();
        var path = Path.Combine(CvFolder, cv.StoredFileName);
        return System.IO.File.Exists(path) ? PhysicalFile(path, cv.ContentType, cv.OriginalFileName) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCv(int id)
    {
        var cv = await db.CvDocuments.FirstOrDefaultAsync(c => c.Id == id && c.UserId == Uid);
        if (cv is null) return NotFound();
        var path = Path.Combine(CvFolder, cv.StoredFileName);
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        db.CvDocuments.Remove(cv);   // applications keep their record; the CV link becomes null
        await db.SaveChangesAsync();
        TempData["Success"] = "Your CV has been deleted.";
        return RedirectToAction(nameof(Cv));
    }

    // ---------- Applications ----------
    [HttpGet]
    public async Task<IActionResult> Apply(int id)
    {
        var o = await db.Opportunities.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (o is null) return NotFound();
        if (!o.IsOpen || !string.IsNullOrEmpty(o.ExternalUrl)) { TempData["Error"] = "This opportunity is not accepting applications through RebuildYOU."; return RedirectToAction("Details", "Opportunities", new { id }); }
        if (await db.Applications.AnyAsync(a => a.OpportunityId == id && a.UserId == Uid)) { TempData["Error"] = "You have already applied for this opportunity."; return RedirectToAction(nameof(MyApplications)); }
        return View(await BuildApply(o));
    }

    [HttpPost]
    public async Task<IActionResult> Apply(int id, ApplyVM m)
    {
        var o = await db.Opportunities.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (o is null) return NotFound();
        if (!o.IsOpen || !string.IsNullOrEmpty(o.ExternalUrl)) { TempData["Error"] = "This opportunity is closed or handled by the employer."; return RedirectToAction("Details", "Opportunities", new { id }); }
        if (await db.Applications.AnyAsync(a => a.OpportunityId == id && a.UserId == Uid)) { TempData["Error"] = "You have already applied for this opportunity."; return RedirectToAction(nameof(MyApplications)); }
        ModelState.Remove("Opportunity"); ModelState.Remove("User");
        if (m.CvId is null || !await db.CvDocuments.AnyAsync(c => c.Id == m.CvId && c.UserId == Uid)) ModelState.AddModelError("", "Please select one of your uploaded CVs.");
        if (!m.Confirm) ModelState.AddModelError("", "Please confirm that your information is accurate.");
        var uploads = new (string Name, IFormFile? File)[]
        {
            ("Matric results / certificate", m.MatricResultsFile),
            ("ID document", m.IdentityDocumentFile),
            ("Proof of address", m.ProofOfAddressFile),
            ("Certificates / qualifications", m.CertificatesFile),
            ("Additional supporting document", m.AdditionalDocumentFile)
        };
        foreach (var upload in uploads)
        {
            if (upload.File is null || upload.File.Length == 0) continue;
            if (upload.File.Length > MaxSupportingDocumentBytes)
                ModelState.AddModelError(upload.File.Name, $"{upload.Name} must be no larger than 5 MB.");
            else if (!await IsValidSupportingDocumentAsync(upload.File))
                ModelState.AddModelError(upload.File.Name, $"{upload.Name} must be a valid PDF, DOCX, JPG or PNG file.");
        }
        if (!ModelState.IsValid)
        {
            var vm = await BuildApply(o);
            vm.CvId = m.CvId; vm.CoverNote = m.CoverNote;
            return View(vm);
        }

        string? stagedDirectory = null;
        var documents = new List<ApplicationFileVM>();
        var selectedUploads = uploads.Where(u => u.File is { Length: > 0 }).ToList();
        if (selectedUploads.Count > 0)
        {
            stagedDirectory = Path.Combine(ApplicationFilesFolder, ".pending-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(stagedDirectory);
                foreach (var upload in selectedUploads)
                {
                    var file = upload.File!;
                    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var document = new ApplicationFileVM
                    {
                        Id = Guid.NewGuid(),
                        Name = upload.Name,
                        OriginalFileName = SafeFileName(file.FileName),
                        StoredFileName = Guid.NewGuid().ToString("N") + extension,
                        ContentType = SupportingDocumentContentType(extension),
                        SizeBytes = file.Length
                    };
                    await using var stream = System.IO.File.Create(Path.Combine(stagedDirectory, document.StoredFileName));
                    await file.CopyToAsync(stream);
                    documents.Add(document);
                }
                await System.IO.File.WriteAllTextAsync(Path.Combine(stagedDirectory, "documents.json"), JsonSerializer.Serialize(documents));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (Directory.Exists(stagedDirectory)) Directory.Delete(stagedDirectory, true);
                TempData["Error"] = "Supporting documents could not be saved. Please try again.";
                return RedirectToAction(nameof(Apply), new { id });
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var application = new JobApplication { OpportunityId = id, UserId = Uid, CvDocumentId = m.CvId, CoverNote = m.CoverNote?.Trim() };
        db.Applications.Add(application);
        try
        {
            await db.SaveChangesAsync();
            if (stagedDirectory is not null)
                Directory.Move(stagedDirectory, Path.Combine(ApplicationFilesFolder, application.Id.ToString()));
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            if (stagedDirectory is not null && Directory.Exists(stagedDirectory)) Directory.Delete(stagedDirectory, true);
            TempData["Error"] = "You have already applied for this opportunity.";
            return RedirectToAction(nameof(MyApplications));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await transaction.RollbackAsync();
            if (stagedDirectory is not null && Directory.Exists(stagedDirectory)) Directory.Delete(stagedDirectory, true);
            var applicationDirectory = Path.Combine(ApplicationFilesFolder, application.Id.ToString());
            if (Directory.Exists(applicationDirectory)) Directory.Delete(applicationDirectory, true);
            TempData["Error"] = "Supporting documents could not be saved. Please try again.";
            return RedirectToAction(nameof(Apply), new { id });
        }
        TempData["Success"] = "Your application has been submitted. Applying does not guarantee employment, but we wish you every success.";
        return RedirectToAction(nameof(MyApplications));
    }

    static async Task<bool> IsValidSupportingDocumentAsync(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".docx" or ".jpg" or ".jpeg" or ".png")) return false;
        var header = new byte[8];
        await using var stream = file.OpenReadStream();
        var length = await stream.ReadAsync(header.AsMemory());
        return extension switch
        {
            ".pdf" => length >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
            ".docx" => length >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04,
            ".jpg" or ".jpeg" => length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => length == 8 && header.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            _ => false
        };
    }

    static string SupportingDocumentContentType(string extension) => extension switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => throw new InvalidOperationException("Unsupported supporting document extension.")
    };

    static string SafeFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        return safeName.Length > 200 ? safeName[^200..] : safeName;
    }

    async Task<ApplyVM> BuildApply(Opportunity o) => new()
    {
        Opportunity = o, User = (await users.GetUserAsync(User))!,
        Cvs = await db.CvDocuments.Where(c => c.UserId == Uid).OrderByDescending(c => c.UploadedAt).ToListAsync()
    };

    public async Task<IActionResult> MyApplications() =>
        View(await db.Applications.Include(a => a.Opportunity).Where(a => a.UserId == Uid).OrderByDescending(a => a.SubmittedAt).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Withdraw(int id)
    {
        var a = await db.Applications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid);
        if (a is null) return NotFound();
        if (a.Status is ApplicationStatus.Submitted or ApplicationStatus.UnderReview) { a.Status = ApplicationStatus.Withdrawn; await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(MyApplications));
    }

    public async Task<IActionResult> Saved() =>
        View(await db.Opportunities.Include(o => o.Category).Where(o => db.SavedOpportunities.Any(s => s.UserId == Uid && s.OpportunityId == o.Id)).ToListAsync());
}
