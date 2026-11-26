using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RebuildYou.Web.Data;
using RebuildYou.Web.Models;
using RebuildYou.Web.Services;

var b = WebApplication.CreateBuilder(args);
var configuredKeyDirectory = b.Configuration["DataProtection:KeyDirectory"];
var keyDirectory = string.IsNullOrWhiteSpace(configuredKeyDirectory)
    ? Path.Combine(b.Environment.ContentRootPath, "App_Data", "data-protection-keys")
    : Path.IsPathRooted(configuredKeyDirectory)
        ? configuredKeyDirectory
        : Path.Combine(b.Environment.ContentRootPath, configuredKeyDirectory);
Directory.CreateDirectory(keyDirectory);
b.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));

b.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(b.Configuration.GetConnectionString("DefaultConnection")));
b.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    foreach (var proxy in b.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    {
        if (!IPAddress.TryParse(proxy, out var address))
            throw new InvalidOperationException($"ReverseProxy:KnownProxies contains an invalid IP address: '{proxy}'.");
        o.KnownProxies.Add(address);
    }
});
b.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 8;
    o.Password.RequireDigit = true;
    o.Password.RequireUppercase = true;
    o.Password.RequireLowercase = true;
    o.Password.RequireNonAlphanumeric = false;
    o.User.RequireUniqueEmail = true;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
b.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));
b.Services.AddTransient<IAppEmailSender, SmtpEmailSender>();

b.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/AccessDenied";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = b.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
});
b.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
b.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("forms", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

var app = b.Build();
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.RunAsync(scope.ServiceProvider, app.Configuration);
}
app.Run();

public partial class Program { }
