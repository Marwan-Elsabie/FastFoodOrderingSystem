using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FastFoodOrderingSystem.Data;
using FastFoodOrderingSystem.Models;
using System.Security.Claims;

namespace FastFoodOrderingSystem.Controllers
{
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WishlistController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var wishlistItems = await _context.WishlistItems
                .Include(w => w.Product)
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.AddedDate)
                .ToListAsync();

            return View(wishlistItems);
        }

        [HttpPost]
        public async Task<IActionResult> AddToWishlist(int productId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if already in wishlist
            var existingItem = await _context.WishlistItems
                .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

            if (existingItem == null)
            {
                var wishlistItem = new WishlistItem
                {
                    UserId = userId,
                    ProductId = productId,
                    AddedDate = DateTime.Now
                };

                _context.WishlistItems.Add(wishlistItem);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Added to wishlist!";
            }
            else
            {
                TempData["InfoMessage"] = "Already in wishlist";
            }

            return RedirectToAction("Details", "Products", new { id = productId });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromWishlist(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var wishlistItem = await _context.WishlistItems.FindAsync(id);

            if (wishlistItem == null || wishlistItem.UserId != userId)
            {
                return NotFound();
            }

            _context.WishlistItems.Remove(wishlistItem);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Removed from wishlist";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult MoveToCart(int id)
        {
            // This would move item from wishlist to cart
            // Implementation similar to RemoveFromWishlist + AddToCart
            TempData["SuccessMessage"] = "Moved to cart!";
            return RedirectToAction("Index");
        }
    }
}