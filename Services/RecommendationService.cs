using FastFoodOrderingSystem.Data;
using FastFoodOrderingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FastFoodOrderingSystem.Services
{
    public class RecommendationService : IRecommendationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RecommendationService> _logger;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        public RecommendationService(ApplicationDbContext context, IMemoryCache cache, ILogger<RecommendationService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<Product>> GetTopSellingAsync(int count = 4, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"top_selling_{count}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;

                var top = await _context.OrderItems
                    .AsNoTracking()
                    .GroupBy(oi => oi.ProductId)
                    .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
                    .OrderByDescending(g => g.Qty)
                    .Take(count)
                    .ToListAsync(cancellationToken);

                var productIds = top.Select(t => t.ProductId).ToList();
                if (!productIds.Any()) return new List<Product>();

                var products = await _context.Products
                    .AsNoTracking()
                    .Where(p => productIds.Contains(p.Id) && p.IsAvailable)
                    .ToListAsync(cancellationToken);

                // preserve order of top by quantity
                var ordered = productIds
                    .Select(id => products.FirstOrDefault(p => p.Id == id))
                    .Where(p => p != null)
                    .Cast<Product>()
                    .ToList();

                return ordered;
            });
        }

        public async Task<List<Product>> GetAlsoBoughtAsync(int productId, int count = 4, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"also_bought_{productId}_{count}";
            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;

                // find orders that contained productId
                var orderIds = await _context.OrderItems
                    .AsNoTracking()
                    .Where(oi => oi.ProductId == productId)
                    .Select(oi => oi.OrderId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                if (!orderIds.Any()) return new List<Product>();

                // aggregate other products in those orders
                var coOccurring = await _context.OrderItems
                    .AsNoTracking()
                    .Where(oi => orderIds.Contains(oi.OrderId) && oi.ProductId != productId)
                    .GroupBy(oi => oi.ProductId)
                    .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
                    .OrderByDescending(g => g.Qty)
                    .Take(count)
                    .ToListAsync(cancellationToken);

                var coProductIds = coOccurring.Select(x => x.ProductId).ToList();
                if (!coProductIds.Any()) return new List<Product>();

                var products = await _context.Products
                    .AsNoTracking()
                    .Where(p => coProductIds.Contains(p.Id) && p.IsAvailable)
                    .ToListAsync(cancellationToken);

                var ordered = coProductIds
                    .Select(id => products.FirstOrDefault(p => p.Id == id))
                    .Where(p => p != null)
                    .Cast<Product>()
                    .ToList();

                return ordered;
            });
        }
    }
}