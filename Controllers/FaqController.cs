using Microsoft.AspNetCore.Mvc;

namespace RebuildYou.Web.Controllers;

/// <summary>The FAQ and help chatbot page, between "About Us" and "Contact Us" in the menu.</summary>
public class FaqController : Controller
{
    public IActionResult Index() => View();
}
