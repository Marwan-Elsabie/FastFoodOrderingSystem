using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FastFoodOrderingSystem.Data;
using FastFoodOrderingSystem.Models;
using System.Security.Claims;

namespace FastFoodOrderingSystem.Controllers
{
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int productId, int rating, string comment)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if user already reviewed this product
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == userId);

            if (existingReview != null)
            {
                // Update existing review
                existingReview.Rating = rating;
                existingReview.Comment = comment;
                existingReview.CreatedDate = DateTime.Now;
            }
            else
            {
                // Create new review
                var review = new Review
                {
                    ProductId = productId,
                    UserId = userId,
                    Rating = rating,
                    Comment = comment,
                    CreatedDate = DateTime.Now
                };
                _context.Reviews.Add(review);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Products", new { id = productId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var review = await _context.Reviews.FindAsync(id);

            if (review == null || review.UserId != userId)
            {
                return NotFound();
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Products", new { id = review.ProductId });
        }
    }
}