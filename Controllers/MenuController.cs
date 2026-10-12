using FarmhouseBakery.Data;
using FarmhouseBakery.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FarmhouseBakery.Controllers
{
    [Authorize(Roles = "Admin")]
    public class MenuController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MenuController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Manage()
        {
            var items = await _context.MenuItems
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(MenuItem item, IFormFile? imageFile)
        {
            if (!ModelState.IsValid)
            {
                var items = await _context.MenuItems
                    .OrderByDescending(x => x.Id)
                    .ToListAsync();

                return View("Manage", items);
            }

            if (item.Id == 0)
            {
                // ADD

                if (imageFile != null && imageFile.Length > 0)
                {
                    item.ImageUrl = await SaveImage(imageFile);
                }

                item.CreatedDate = DateTime.UtcNow;

                _context.MenuItems.Add(item);
            }
            else
            {
                // EDIT

                var existingItem = await _context.MenuItems
                    .FindAsync(item.Id);

                if (existingItem == null)
                {
                    return NotFound();
                }

                existingItem.Name = item.Name;
                existingItem.Price = item.Price;
                existingItem.Ingredients = item.Ingredients;
                existingItem.Category = item.Category;

                // Keep old image unless a new one is selected
                if (imageFile != null && imageFile.Length > 0)
                {
                    existingItem.ImageUrl = await SaveImage(imageFile);
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Manage));
        }

        private async Task<string> SaveImage(IFormFile imageFile)
        {
            var uploadsFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "images"
            );

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = Guid.NewGuid().ToString()
                           + Path.GetExtension(imageFile.FileName);

            var filePath = Path.Combine(
                uploadsFolder,
                fileName
            );

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return "/images/" + fileName;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);

            if (item == null)
            {
                return NotFound();
            }

            _context.MenuItems.Remove(item);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Manage));
        }
    }
}