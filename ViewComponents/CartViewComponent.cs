using Microsoft.AspNetCore.Mvc;
using FastFoodOrderingSystem.Helpers;

namespace FastFoodOrderingSystem.ViewComponents
{
    public class CartViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            var cart = HttpContext.Session.GetObject<List<Models.ShoppingCartItem>>("Cart");
            var cartCount = cart?.Sum(item => item.Quantity) ?? 0;
            return View(cartCount);
        }
    }
}