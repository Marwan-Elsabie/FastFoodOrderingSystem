# Fast Food Ordering System

A complete ASP.NET Core MVC fast food ordering system with admin panel.

## Features
- User registration & authentication
- Product catalog with search/filter
- Shopping cart & checkout
- Order management
- Admin dashboard
- Email notifications
- Reviews & ratings
- Wishlist
- Export functionality
- Responsive design

## Setup Instructions
1. Clone repository
2. Set the connection string via user secrets (keeps it out of source control):
   `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=FastFoodOrderingSystemDb;Trusted_Connection=True;MultipleActiveResultSets=true"`
3. Run application: `dotnet run` — the database is created and seeded automatically on startup (see `Data/DbInitializer.cs`)

## Admin Credentials
- Email: admin@fastfood.com
- Password: Admin@123

## Technologies
- ASP.NET Core MVC
- Entity Framework Core
- SQL Server
- Bootstrap 5
- MailKit (for emails)
- ClosedXML (for Excel exports)