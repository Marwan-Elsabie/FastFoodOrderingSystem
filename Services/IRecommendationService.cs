using FastFoodOrderingSystem.Models;

namespace FastFoodOrderingSystem.Services
{
    public interface IRecommendationService
    {
        Task<List<Product>> GetTopSellingAsync(int count = 4, CancellationToken cancellationToken = default);
        Task<List<Product>> GetAlsoBoughtAsync(int productId, int count = 4, CancellationToken cancellationToken = default);
    }
}