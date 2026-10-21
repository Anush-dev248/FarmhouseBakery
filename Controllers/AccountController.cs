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
            string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                ModelState.AddModelError(
                    "", "Invalid email or password.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                password,
                isPersistent: false,
                lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        return RedirectToAction("AdminIndex", "Home");
                    }

                    // Normal users go straight to menu management.
                    return RedirectToAction("Manage", "Menu");
                }

            ModelState.AddModelError(
                "", "Invalid email or password.");

            return View();
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