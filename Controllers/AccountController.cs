using FarmhouseBakery.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using FarmhouseBakery.Services;

using FarmhouseBakery.Data;

namespace FarmhouseBakery.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly EmailService _emailService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        EmailService emailService,
        ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login(
            string email, string password, bool rememberMe = false)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                ModelState.AddModelError(
                    "", "Invalid email or password.");
                return View();
            }

            // "Remember me" = persistent login (14 days, see Program.cs); otherwise 2 hours.
            var result = await _signInManager.PasswordSignInAsync(
                user,
                password,
                isPersistent: rememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                return await RedirectAfterLogin(user);
            }

            ModelState.AddModelError("", result.IsLockedOut
                ? "Too many wrong attempts. This account is locked for a few minutes."
                : "Invalid email or password.");

            return View();
        }

        private async Task<IActionResult> RedirectAfterLogin(IdentityUser user)
        {
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return RedirectToAction("AdminIndex", "Home");
            }

            // Normal users go to their own dashboard.
            return RedirectToAction("Dashboard", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Register()
        {
            // Allow the first account to be created.
            if (!await _userManager.Users.AnyAsync())
                return View();

            // After setup, require an authenticated administrator.
            if (User.Identity?.IsAuthenticated != true ||
                !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register(
            RegisterViewModel model)
        {
            bool firstAccount = !await _userManager.Users.AnyAsync();

            if (!firstAccount &&
                (User.Identity?.IsAuthenticated != true ||
                 !User.IsInRole("Admin")))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
                return View(model);

            var user = new IdentityUser
            {
                UserName = model.Email,
                Email = model.Email
            };

            var result = await _userManager.CreateAsync(
                user, model.Password);

            if (result.Succeeded)
            {
                if (firstAccount)
                {
                    // The first account becomes administrator.
                    var additionalRoleResult = await _userManager.AddToRoleAsync(user, "Admin");

                    if (!additionalRoleResult.Succeeded)
                    {
                        foreach (var error in additionalRoleResult.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }

                        return View(model);
                    }

                    return RedirectToAction("Index", "Home");
                }

                // Additional accounts are administrators only when the admin ticks the box.
                if (model.IsAdmin)
                {
                    var roleResult = await _userManager.AddToRoleAsync(user, "Admin");

                    if (!roleResult.Succeeded)
                    {
                        foreach (var error in roleResult.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }

                        return View(model);
                    }
                }

                return RedirectToAction("AdminIndex", "Home");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var result = await _userManager.ChangePasswordAsync(
                user, model.CurrentPassword, model.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return View(model);
            }

            // Keep this browser logged in after the password change.
            await _signInManager.RefreshSignInAsync(user);

            TempData["Message"] = "Your password has been changed.";

            return RedirectToAction(nameof(ChangePassword));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        
[HttpGet]
[AllowAnonymous]
public IActionResult ForgotPassword()
{
    return View();
}

[HttpPost]
[AllowAnonymous]
[ValidateAntiForgeryToken]
[EnableRateLimiting("auth")]
public async Task<IActionResult> ForgotPassword(string email)
{
    if (string.IsNullOrWhiteSpace(email))
    {
        ModelState.AddModelError("", "Please enter your email.");
        return View();
    }

    var user = await _userManager.FindByEmailAsync(email);

    if (user == null)
    {
        return View("ForgotPasswordConfirmation");
    }

    var token = await _userManager.GeneratePasswordResetTokenAsync(user);

    var resetLink = Url.Action(
        "ResetPassword",
        "Account",
        new { email = user.Email, token = token },
        Request.Scheme);

    if (string.IsNullOrEmpty(resetLink))
    {
        return View("ForgotPasswordConfirmation");
    }

    try
    {
        await _emailService.SendEmailAsync(
            user.Email!,
            "Reset Your Farmhouse Bakery Password",
            "Click this link to reset your password:\n\n" + resetLink);
    }

    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Failed to send password reset email.");

        ModelState.AddModelError(
            "",
            "We couldn't send the password reset email. Please try again later.");

        return View();
    }


    return View("ForgotPasswordConfirmation");
}

[HttpGet]
[AllowAnonymous]
public IActionResult ResetPassword(string email, string token)
{
    var model = new ResetPasswordViewModel
    {
        Email = email,
        Token = token
    };

    return View(model);
}

[HttpPost]
[AllowAnonymous]
[ValidateAntiForgeryToken]
[EnableRateLimiting("auth")]
public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
{
    if (!ModelState.IsValid)
    {
        return View(model);
    }

    var user = await _userManager.FindByEmailAsync(model.Email);

    if (user == null)
    {
        return View("ForgotPasswordConfirmation");
    }

    var result = await _userManager.ResetPasswordAsync(
        user,
        model.Token,
        model.Password
    );


    if (result.Succeeded)
    {
        return RedirectToAction("Login");
    }


    foreach (var error in result.Errors)
    {
        ModelState.AddModelError("", error.Description);
    }

    return View(model);
}
    }
}