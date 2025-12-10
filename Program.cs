using FastFoodOrderingSystem.Data;
using FastFoodOrderingSystem.Middleware;
using FastFoodOrderingSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddSession(); // Add session for shopping cart
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true)
    .AddEnvironmentVariables();

// Configure Stripe API key at startup for non-request contexts (optional)
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Stripe webhook endpoint - ensure route is reachable
app.MapPost("/api/payment/webhook", async (HttpRequest request, ApplicationDbContext db, IConfiguration config, IEmailService emailService, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("StripeWebhook");
    var json = await new StreamReader(request.Body).ReadToEndAsync();
    var webhookSecret = config["Stripe:WebhookSecret"];
    try
    {
        var stripeEvent = EventUtility.ConstructEvent(
            json,
            request.Headers["Stripe-Signature"],
            webhookSecret
        );

        if (stripeEvent.Type == Events.CheckoutSessionCompleted)
        {
            var session = stripeEvent.Data.Object as Session;
            var metadata = session?.Metadata;
            if (metadata != null && metadata.TryGetValue("pendingPaymentId", out var pendingIdStr) && int.TryParse(pendingIdStr, out var pendingId))
            {
                var pending = await db.PendingPayments.FindAsync(pendingId);
                if (pending != null)
                {
                    var cart = System.Text.Json.JsonSerializer.Deserialize<List<FastFoodOrderingSystem.Models.ShoppingCartItem>>(pending.CartJson) ?? new List<FastFoodOrderingSystem.Models.ShoppingCartItem>();

                    await using var tx = await db.Database.BeginTransactionAsync();
                    try
                    {
                        var order = new FastFoodOrderingSystem.Models.Order
                        {
                            UserId = pending.UserId,
                            TotalAmount = pending.Amount,
                            DeliveryAddress = pending.DeliveryAddress,
                            PhoneNumber = pending.PhoneNumber,
                            CustomerName = pending.CustomerName,
                            Status = "Processing",
                            OrderDate = DateTime.Now
                        };
                        db.Orders.Add(order);
                        await db.SaveChangesAsync();

                        var productIds = cart.Select(i => i.ProductId).ToList();
                        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p);

                        foreach (var item in cart)
                        {
                            if (products.ContainsKey(item.ProductId))
                            {
                                db.OrderItems.Add(new FastFoodOrderingSystem.Models.OrderItem
                                {
                                    OrderId = order.Id,
                                    ProductId = item.ProductId,
                                    Quantity = item.Quantity,
                                    UnitPrice = products[item.ProductId].Price
                                });
                            }
                        }

                        db.AuditLogs.Add(new FastFoodOrderingSystem.Models.AuditLog
                        {
                            UserId = pending.UserId,
                            Action = "CreateOrder(Stripe)",
                            Entity = "Order",
                            EntityId = order.Id,
                            Details = $"Order created via Stripe Checkout, amount {pending.Amount:C}"
                        });

                        db.PendingPayments.Remove(pending);
                        await db.SaveChangesAsync();
                        await tx.CommitAsync();

                        // Send confirmation email (best effort)
                        try
                        {
                            if (!string.IsNullOrEmpty(pending.UserId))
                            {
                                var user = await db.Users.FindAsync(pending.UserId);
                                var email = user?.Email;
                                if (!string.IsNullOrEmpty(email))
                                {
                                    await emailService.SendOrderConfirmationAsync(email, pending.CustomerName, order.Id, order.TotalAmount);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error sending email after Stripe webhook for order {OrderId}", order.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        await tx.RollbackAsync();
                        logger.LogError(ex, "Failed to create order from pending payment {PendingId}", pending.Id);
                    }
                }
            }
        }

        return Results.Ok();
    }
    catch (StripeException ex)
    {
        logger.LogError(ex, "Stripe webhook error");
        return Results.BadRequest();
    }
});

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await DbInitializer.Initialize(context, userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.UseMiddleware<ExceptionMiddleware>();

app.Run();