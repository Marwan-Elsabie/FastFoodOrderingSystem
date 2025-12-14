using FastFoodOrderingSystem.Data;
using FastFoodOrderingSystem.Middleware;
using FastFoodOrderingSystem.Models;
using FastFoodOrderingSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using Microsoft.AspNetCore.Identity.UI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSession(); // Add session for shopping cart
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IEmailSender, NullEmailSender>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// New: register memory cache and recommendation service
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

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

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    await DbInitializer.Initialize(context, userManager, roleManager);
}

app.UseMiddleware<ExceptionMiddleware>();

app.Run();