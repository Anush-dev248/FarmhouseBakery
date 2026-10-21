using FarmhouseBakery.Data;
using FarmhouseBakery.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FarmhouseBakery.Controllers
{
    // Any logged-in user can add, edit and delete menu items.
    [Authorize]
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

        private const long MaxImageBytes = 5 * 1024 * 1024;

        private static readonly string[] AllowedImageExtensions =
            { ".jpg", ".jpeg", ".png", ".webp" };

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxImageBytes + 1024 * 1024)]
        public async Task<IActionResult> Save(MenuItem item, IFormFile? imageFile)
        {
            if (imageFile != null && imageFile.Length > 0)
            {
                var imageError = await ValidateImage(imageFile);

                if (imageError != null)
                {
                    ModelState.AddModelError("imageFile", imageError);
                }
            }

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

        // Returns an error message, or null when the file is a real JPG / PNG / WEBP image.
        private static async Task<string?> ValidateImage(IFormFile imageFile)
        {
            if (imageFile.Length > MaxImageBytes)
            {
                return "Image must be 5 MB or smaller.";
            }

            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

            if (!AllowedImageExtensions.Contains(extension))
            {
                return "Only JPG, PNG or WEBP images are allowed.";
            }

            // Check the first bytes of the file so a renamed file can't pass as an image.
            var header = new byte[12];

            using (var stream = imageFile.OpenReadStream())
            {
                int read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);

                if (read < header.Length)
                {
                    return "The selected file is not a valid image.";
                }
            }

            bool isJpg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
            bool isWebp = header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                          && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

            bool matches = extension switch
            {
                ".jpg" or ".jpeg" => isJpg,
                ".png" => isPng,
                ".webp" => isWebp,
                _ => false
            };

            return matches ? null : "The selected file is not a valid image.";
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
                           + Path.GetExtension(imageFile.FileName).ToLowerInvariant();

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