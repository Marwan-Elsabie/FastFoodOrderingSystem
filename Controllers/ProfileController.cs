using FastFoodOrderingSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FastFoodOrderingSystem.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var claims = await _userManager.GetClaimsAsync(user);

            var addressClaim = claims.FirstOrDefault(c => c.Type == "DeliveryAddress");

            var model = new ProfileViewModel
            {
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                DeliveryAddress = addressClaim?.Value ?? string.Empty
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Index");

            // Update phone number
            user.PhoneNumber = model.PhoneNumber;
            await _userManager.UpdateAsync(user);

            // Save delivery address as a claim
            var claims = await _userManager.GetClaimsAsync(user);
            var existingAddress = claims.FirstOrDefault(c =>
                c.Type == "delivery_address" || c.Type == "DeliveryAddress");

            if (existingAddress != null)
                await _userManager.RemoveClaimAsync(user, existingAddress);

            if (!string.IsNullOrWhiteSpace(model.DeliveryAddress))
                await _userManager.AddClaimAsync(user, new Claim("delivery_address", model.DeliveryAddress));

            // IMPORTANT: Refresh user session so Checkout loads the claim
            await _signInManager.RefreshSignInAsync(user);

            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("Index");
        }
    }
}
