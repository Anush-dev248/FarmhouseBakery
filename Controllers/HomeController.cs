using System.Diagnostics;
using FarmhouseBakery.Data;
using FarmhouseBakery.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace FarmhouseBakery.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var menuItems = await _context.MenuItems
            .OrderByDescending(x => x.Id)
            .Take(3)
            .ToListAsync();

        return View(menuItems);
    }
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminIndex()
    {
        var menuItems = await _context.MenuItems
            .OrderByDescending(x => x.Id)
            .Take(3)
            .ToListAsync();

        return View(menuItems);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public async Task<IActionResult> Menu()
    {
        var menuItems = await _context.MenuItems
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return View(menuItems);
    }

    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id
                ?? HttpContext.TraceIdentifier
        });
    }
}