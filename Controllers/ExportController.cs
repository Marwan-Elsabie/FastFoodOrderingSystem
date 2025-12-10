using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FastFoodOrderingSystem.Data;
using System.Text;
using ClosedXML.Excel;

namespace FastFoodOrderingSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ExportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExportController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> ExportOrders(DateTime? startDate, DateTime? endDate)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => (!startDate.HasValue || o.OrderDate >= startDate) &&
                           (!endDate.HasValue || o.OrderDate <= endDate))
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Orders");

            // Headers
            worksheet.Cell(1, 1).Value = "Order ID";
            worksheet.Cell(1, 2).Value = "Customer";
            worksheet.Cell(1, 3).Value = "Date";
            worksheet.Cell(1, 4).Value = "Total";
            worksheet.Cell(1, 5).Value = "Status";
            worksheet.Cell(1, 6).Value = "Items";

            // Data
            int row = 2;
            foreach (var order in orders)
            {
                worksheet.Cell(row, 1).Value = order.Id;
                worksheet.Cell(row, 2).Value = order.CustomerName;
                worksheet.Cell(row, 3).Value = order.OrderDate;
                worksheet.Cell(row, 4).Value = order.TotalAmount;
                worksheet.Cell(row, 5).Value = order.Status;

                var items = string.Join(", ", order.OrderItems.Select(oi =>
                    $"{oi.Product.Name} x{oi.Quantity}"));
                worksheet.Cell(row, 6).Value = items;

                row++;
            }

            // Format
            worksheet.Columns().AdjustToContents();

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Orders_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        public async Task<IActionResult> ExportProducts()
        {
            var products = await _context.Products.ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Products");

            // Headers
            worksheet.Cell(1, 1).Value = "ID";
            worksheet.Cell(1, 2).Value = "Name";
            worksheet.Cell(1, 3).Value = "Category";
            worksheet.Cell(1, 4).Value = "Price";
            worksheet.Cell(1, 5).Value = "Available";

            // Data
            int row = 2;
            foreach (var product in products)
            {
                worksheet.Cell(row, 1).Value = product.Id;
                worksheet.Cell(row, 2).Value = product.Name;
                worksheet.Cell(row, 3).Value = product.Category;
                worksheet.Cell(row, 4).Value = product.Price;
                worksheet.Cell(row, 5).Value = product.IsAvailable ? "Yes" : "No";

                row++;
            }

            worksheet.Columns().AdjustToContents();

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Products_{DateTime.Now:yyyyMMdd}.xlsx");
        }
    }
}