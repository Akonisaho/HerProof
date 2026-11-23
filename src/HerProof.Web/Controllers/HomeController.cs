using System.Diagnostics;
using HerProof.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HerProof.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>A real, scannable QR code for the homepage's credential mockup — it genuinely opens
    /// this live app, the same way a real Skills Passport share link's QR code does (PassportController
    /// has the real one). Not a static image: generated the same way every time, with the same library.</summary>
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public IActionResult BrandQr()
    {
        var url = Url.Action(nameof(Index), "Home", null, Request.Scheme)!;
        return File(QrCodeGenerator.GeneratePng(url), "image/png");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [Route("Home/StatusCode/{code:int}")]
    public IActionResult StatusCodePage(int code)
    {
        Response.StatusCode = code;
        return View("StatusCode", code);
    }
}
