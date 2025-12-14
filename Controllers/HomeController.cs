using FastFoodOrderingSystem.Data;
using FastFoodOrderingSystem.Helpers;
using FastFoodOrderingSystem.Models;
using FastFoodOrderingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace FastFoodOrderingSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IRecommendationService _recommendationService;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IMemoryCache cache, IRecommendationService recommendationService)
        {
            _logger = logger;
            _context = context;
            _cache = cache;
            _recommendationService = recommendationService;
        }

        public async Task<IActionResult> Index()
        {
            const string cacheKey = "FeaturedProducts";

            if (!_cache.TryGetValue(cacheKey, out List<Product> products))
            {
                products = await _context.Products
                    .Where(p => p.IsAvailable)
                    .Take(8)
                    .ToListAsync();

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(10));

                _cache.Set(cacheKey, products, cacheOptions);
            }

            return View(products);
        }

        public async Task<IActionResult> Menu(string searchTerm, string category, int pageIndex = 1, int pageSize = 8)
        {
            var products = _context.Products
                .Where(p => p.IsAvailable)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                products = products.Where(p =>
                    p.Name.Contains(searchTerm) ||
                    p.Description.Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(category) && category != "All")
            {
                products = products.Where(p => p.Category == category);
            }

            // Get distinct categories for filter dropdown
            ViewBag.Categories = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .ToListAsync();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.Category = category;

            // New: Top selling products for the menu page
            var topSelling = await _recommendationService.GetTopSellingAsync(4);
            ViewBag.TopSelling = topSelling;

            var paginatedProducts = await PaginatedList<Product>.CreateAsync(products, pageIndex, pageSize);
            return View(paginatedProducts);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<IActionResult> Search(string searchTerm, string category)
        {
            ViewData["SearchTerm"] = searchTerm;
            ViewData["Category"] = category;

            var products = _context.Products
                .Where(p => p.IsAvailable)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                products = products.Where(p =>
                    p.Name.Contains(searchTerm) ||
                    p.Description.Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(category) && category != "All")
            {
                products = products.Where(p => p.Category == category);
            }

            // Get distinct categories for filter dropdown
            ViewBag.Categories = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .ToListAsync();

            return View("Menu", await products.ToListAsync());
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Contact(ContactViewModel model)
        {
            if (ModelState.IsValid)
            {
                // In a real application, you would send an email here
                TempData["SuccessMessage"] = "Thank you for your message! We'll get back to you soon.";
                return RedirectToAction("Contact");
            }
            return View(model);
        }
    }
}