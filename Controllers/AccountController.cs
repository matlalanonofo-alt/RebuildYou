using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RebuildYou.Web.Data;
using RebuildYou.Web.Models;

namespace RebuildYou.Web.Controllers;

public class AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, AppDbContext db) : Controller
{
    static string Norm(string p) { var d = new string(p.Where(char.IsDigit).ToArray()); return "+" + (d.StartsWith("0") ? "27" + d.Substring(1) : d); }

    [HttpGet] public IActionResult Register() => View(new RegisterVM());

    [HttpPost, EnableRateLimiting("forms")]
    public async Task<IActionResult> Register(RegisterVM m)
    {
        if (!m.AcceptPrivacy) ModelState.AddModelError(nameof(m.AcceptPrivacy), "Please tick the box to continue.");
        if (!ModelState.IsValid) return View(m);
        var email = m.Email.Trim(); var phone = Norm(m.Mobile);
        if (await users.FindByEmailAsync(email) is not null || users.Users.Any(x => x.PhoneNumber == phone))
        {
            ModelState.AddModelError("", "You already have a RebuildYOU profile. Please log in instead.");
            ViewBag.Exists = true; return View(m);
        }
        var u = new ApplicationUser { UserName = email, Email = email, PhoneNumber = phone, FirstName = m.FirstName.Trim(), Surname = m.Surname.Trim(), AcceptedPrivacy = m.AcceptPrivacy };
        var res = await users.CreateAsync(u, m.Password);   // password is hashed by Identity
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors)
                ModelState.AddModelError("", e.Code is "DuplicateUserName" or "DuplicateEmail" ? "An account with this email address already exists." : e.Description);
            return View(m);
        }
        await users.AddToRoleAsync(u, "User");
        db.LoginRecords.Add(new LoginRecord { UserId = u.Id, Email = u.Email!, Event = "Registered", Ip = HttpContext.Connection.RemoteIpAddress?.ToString() });
        u.LastLoginAt = DateTime.UtcNow; u.LoginCount = 1; await users.UpdateAsync(u); await db.SaveChangesAsync();              // role is never taken from the form
        await signIn.SignInAsync(u, isPersistent: false);
        TempData["Success"] = $"Welcome to RebuildYOU, {u.FirstName}! Your account has been created.";
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet] public IActionResult Login(string? returnUrl = null) => View(new LoginVM { ReturnUrl = returnUrl });

    [HttpPost, EnableRateLimiting("forms")]
    public async Task<IActionResult> Login(LoginVM m)
    {
        if (!ModelState.IsValid) return View(m);
        var u = await users.FindByEmailAsync(m.Email.Trim());
        if (u is not null && !u.IsActive) { ModelState.AddModelError("", "This account has been suspended. Please contact RebuildYOU."); return View(m); }
        var res = u is null ? Microsoft.AspNetCore.Identity.SignInResult.Failed
                            : await signIn.PasswordSignInAsync(u, m.Password, m.RememberMe, lockoutOnFailure: true);
        db.LoginRecords.Add(new LoginRecord { UserId = u?.Id, Email = m.Email.Trim(), Event = res.Succeeded ? "Login" : res.IsLockedOut ? "Locked out" : "Failed login", Ip = HttpContext.Connection.RemoteIpAddress?.ToString() });
        if (res.Succeeded && u != null) { u.LastLoginAt = DateTime.UtcNow; u.LoginCount++; await users.UpdateAsync(u); }
        await db.SaveChangesAsync();
        if (res.Succeeded)
        {
            if (!string.IsNullOrEmpty(m.ReturnUrl) && Url.IsLocalUrl(m.ReturnUrl)) return LocalRedirect(m.ReturnUrl);
            return await users.IsInRoleAsync(u!, "Admin") ? RedirectToAction("Index", "Admin") : RedirectToAction("Index", "Dashboard");
        }
        ModelState.AddModelError("", res.IsLockedOut ? "Too many failed attempts. Try again in 15 minutes." : "Incorrect email address or password.");
        return View(m);
    }

    [HttpPost] public async Task<IActionResult> Logout() { await signIn.SignOutAsync(); return RedirectToAction("Index", "Home"); }
    public IActionResult AccessDenied() => View();

    [Authorize, HttpGet]
    public async Task<IActionResult> Profile()
    {
        var u = (await users.GetUserAsync(User))!;
        return View(new ProfileVM { FirstName = u.FirstName, Surname = u.Surname, Mobile = u.PhoneNumber ?? "", City = u.City ?? "", EducationLevel = u.EducationLevel ?? "", CareerInterests = u.CareerInterests, EmploymentStatus = u.EmploymentStatus ?? "", TrainingInterests = u.TrainingInterests });
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> Profile(ProfileVM m)
    {
        if (!ModelState.IsValid) return View(m);
        var u = (await users.GetUserAsync(User))!;   // always the signed-in user, never an id from the request
        var phone = Norm(m.Mobile);
        if (users.Users.Any(x => x.PhoneNumber == phone && x.Id != u.Id)) { ModelState.AddModelError("Mobile", "That mobile number belongs to another profile."); return View(m); }
        u.FirstName = m.FirstName.Trim(); u.Surname = m.Surname.Trim(); u.PhoneNumber = phone; u.City = m.City?.Trim();
        u.EducationLevel = m.EducationLevel; u.CareerInterests = m.CareerInterests; u.EmploymentStatus = m.EmploymentStatus; u.TrainingInterests = m.TrainingInterests;
        await users.UpdateAsync(u);
        TempData["Success"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize, HttpGet] public IActionResult ChangePassword() => View(new ChangePasswordVM());

    [Authorize, HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordVM m)
    {
        if (!ModelState.IsValid) return View(m);
        var u = (await users.GetUserAsync(User))!;
        var res = await users.ChangePasswordAsync(u, m.Current, m.New);
        if (!res.Succeeded) { foreach (var e in res.Errors) ModelState.AddModelError("", e.Description); return View(m); }
        await signIn.RefreshSignInAsync(u);
        TempData["Success"] = "Your password has been changed.";
        return RedirectToAction(nameof(Profile));
    }
}
