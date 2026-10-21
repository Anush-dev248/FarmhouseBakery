using FarmhouseBakery.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FarmhouseBakery.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;

        public UsersController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User);

            var adminIds = (await _userManager.GetUsersInRoleAsync("Admin"))
                .Select(u => u.Id)
                .ToHashSet();

            var users = await _userManager.Users
                .OrderBy(u => u.Email)
                .ToListAsync();

            var model = users.Select(u => new UserListItemViewModel
            {
                Id = u.Id,
                Email = u.Email ?? u.UserName ?? "",
                IsAdmin = adminIds.Contains(u.Id),
                IsCurrentUser = u.Id == currentUserId,
                IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow
            }).ToList();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAdmin(string id, bool makeAdmin)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            if (makeAdmin)
            {
                if (!await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    await _userManager.AddToRoleAsync(user, "Admin");
                }

                TempData["Message"] = $"{user.Email} is now an admin.";
            }
            else
            {
                // Don't let an admin lock themselves out, or leave the site with no admin.
                if (user.Id == _userManager.GetUserId(User))
                {
                    TempData["Error"] = "You can't remove admin rights from your own account.";
                    return RedirectToAction(nameof(Index));
                }

                var admins = await _userManager.GetUsersInRoleAsync("Admin");

                if (admins.Count <= 1)
                {
                    TempData["Error"] = "There must be at least one admin.";
                    return RedirectToAction(nameof(Index));
                }

                await _userManager.RemoveFromRoleAsync(user, "Admin");

                TempData["Message"] = $"{user.Email} is no longer an admin.";
            }

            // Makes the change apply to that user's login within a minute (see Program.cs).
            await _userManager.UpdateSecurityStampAsync(user);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["Error"] = "You can't delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsInRoleAsync(user, "Admin") &&
                (await _userManager.GetUsersInRoleAsync("Admin")).Count <= 1)
            {
                TempData["Error"] = "There must be at least one admin.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.DeleteAsync(user);

            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? $"{user.Email} has been deleted."
                : "Could not delete this user. Please try again.";

            return RedirectToAction(nameof(Index));
        }
    }
}
