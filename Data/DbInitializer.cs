using Microsoft.AspNetCore.Identity;
using FastFoodOrderingSystem.Models;

namespace FastFoodOrderingSystem.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            context.Database.EnsureCreated();

            // Create roles
            string[] roleNames = { "Admin", "Customer" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Create admin user
            var adminEmail = "admin@fastfood.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = "Admin",
                    DeliveryAddress = ""
                };
                await userManager.CreateAsync(adminUser, "Admin@123");
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // Seed products if none exist
            if (!context.Products.Any())
            {
                var products = new[]
                {
                    new Product
                    {
                        Name = "Classic Burger",
                        Description = "Juicy beef patty with lettuce, tomato, and special sauce",
                        Price = 5.99m,
                        Category = "Burgers",
                        ImageUrl = "https://images.unsplash.com/photo-1568901346375-23c9450c58cd?w=400",
                        IsAvailable = true
                    },
                    new Product
                    {
                        Name = "Cheese Pizza",
                        Description = "12-inch pizza with mozzarella cheese and tomato sauce",
                        Price = 12.99m,
                        Category = "Pizza",
                        ImageUrl = "https://images.unsplash.com/photo-1565299624946-b28f40a0ae38?w=400",
                        IsAvailable = true
                    },
                    new Product
                    {
                        Name = "French Fries",
                        Description = "Crispy golden fries with a pinch of salt",
                        Price = 3.49m,
                        Category = "Sides",
                        ImageUrl = "https://images.unsplash.com/photo-1573080496219-bb080dd4f877?w-400",
                        IsAvailable = true
                    },
                    new Product
                    {
                        Name = "Cola Drink",
                        Description = "Refreshing cola beverage",
                        Price = 1.99m,
                        Category = "Drinks",
                        ImageUrl = "https://images.unsplash.com/photo-1622483767028-3f66f32aef97?w=400",
                        IsAvailable = true
                    }
                };

                context.Products.AddRange(products);
                await context.SaveChangesAsync();
            }
        }
    }
}