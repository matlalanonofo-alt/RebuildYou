using Microsoft.AspNetCore.Mvc;

namespace RebuildYou.Web.Controllers;

/// <summary>The landing screen is now the top of the Home page. Old /Welcome links go there.</summary>
public class WelcomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Home");
}
